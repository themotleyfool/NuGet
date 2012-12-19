using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Common.Logging;
using Lucene.Net.Linq;
using Ninject;
using NuGet.Server.DataServices;
using LuceneDirectory = Lucene.Net.Store.Directory;

namespace NuGet.Server.Infrastructure.Lucene
{
    public class LucenePackageRepository : ServerPackageRepository, ILucenePackageRepository, IInitializable
    {
        private static readonly ILog Log = LogManager.GetLogger<LucenePackageRepository>();

        static LucenePackageRepository()
        {
            Mapper.CreateMap<IPackage, LucenePackage>().ForMember(dest => dest.Version, opt => opt.MapFrom(src => new StrictSemanticVersion(src.Version.ToString())));
            Mapper.CreateMap<DerivedPackageData, LucenePackage>();
            Mapper.CreateMap<LucenePackage, DerivedPackageData>();
        }

        [Inject]
        public LuceneDataProvider LuceneDataProvider { get; set; }

        [Inject]
        public IQueryable<LucenePackage> LucenePackages { get; set; }

        [Inject]
        public IPackageIndexer Indexer { get; set; }

        private volatile int maxDownloadCount;
        public int MaxDownloadCount { get { return maxDownloadCount; } }

        public LucenePackageRepository(IPackagePathResolver packageResolver, IFileSystem fileSystem)
            : base(packageResolver, fileSystem)
        {
        }

        public void Initialize()
        {
            LuceneDataProvider.RegisterCacheWarmingCallback(UpdateMaxDownloadCount, () => new LucenePackage(FileSystem));

            UpdateMaxDownloadCount(LucenePackages);
        }

        public override async void AddPackage(IPackage package)
        {
            Log.Info(m => m("Adding package {0} {1} to file system", package.Id, package.Version));

            base.AddPackage(package);

            Log.Info(m => m("Indexing package {0} {1}", package.Id, package.Version));

            await Indexer.AddPackage(Convert(package));
        }

        public override void IncrementDownloadCount(IPackage package)
        {
            Indexer.IncrementDownloadCount(package);
        }

        private void UpdateMaxDownloadCount(IQueryable<LucenePackage> packages)
        {
            if (packages.Any())
            {
                maxDownloadCount = packages.Max(p => p.DownloadCount);
            }
            else
            {
                maxDownloadCount = 0;
            }

            Log.Info(m => m("Max download count: " + maxDownloadCount));
        }

        public override IQueryable<IPackage> GetPackages()
        {
            return LucenePackages;
        }

        public override IPackage FindPackage(string packageId, SemanticVersion version)
        {
            var packages = LucenePackages;

            // TODO: Is version ever a range?
            var matches = (IEnumerable<IPackage>)(from p in packages where p.Id == packageId select p).ToArray();
            matches = matches.Where(p => p.Version == version);
            return matches.SingleOrDefault();
        }

        public override IEnumerable<IPackage> FindPackagesById(string packageId)
        {
            return LucenePackages.Where(p => p.Id == packageId);
        }

        public override IQueryable<IPackage> Search(string searchTerm, IEnumerable<string> targetFrameworks, bool allowPrereleaseVersions)
        {
            var packages = LucenePackages;

            if (!string.IsNullOrEmpty(searchTerm))
            {
                packages = from
                                pkg in packages
                           where
                                ((pkg.Id == searchTerm || pkg.Title == searchTerm).Boost(3) ||
                                (pkg.Tags == searchTerm).Boost(2) ||
                                (pkg.Authors.Contains(searchTerm) || pkg.Owners.Contains(searchTerm)).Boost(2) ||
                                (pkg.Summary == searchTerm || pkg.Description == searchTerm)).AllowSpecialCharacters()
                           select
                               pkg;
            }

            if (!allowPrereleaseVersions)
            {
                packages = packages.Where(p => !p.IsPrerelease);
            }

            packages = packages.OrderBy(p => p.Score()).Boost(BoostByDownloadCount);

            return packages;
        }

        public float BoostByDownloadCount(LucenePackage p)
        {
            return ((float)(p.DownloadCount + 1) / (MaxDownloadCount + 1)) * 2;
        }

        public override Package GetMetadataPackage(IPackage package)
        {
            if (!(package is LucenePackage)) throw new ArgumentException("Package of type " + package.GetType() + " not supported.");

            var lucenePackage = (LucenePackage)package;

            var derived = Mapper.Map<LucenePackage, DerivedPackageData>(lucenePackage);

            return new Package(package, derived) { Score = lucenePackage.Score };
        }

        public LucenePackage LoadFromIndex(string path)
        {
            var relativePath = MakeRelative(path);
            var results = from p in LucenePackages where p.Path == relativePath select p;
            return results.SingleOrDefault();
        }

        private string MakeRelative(string path)
        {
            var root = FileSystem.Root;
            if (root.Last() != Path.DirectorySeparatorChar)
            {
                root += Path.DirectorySeparatorChar;
            }

            if (path.StartsWith(root, StringComparison.InvariantCultureIgnoreCase))
            {
                return path.Substring(root.Length);
            }

            throw new ArgumentException("The path " + path + " is not rooted in " + root);
        }

        public LucenePackage LoadFromFileSystem(string path)
        {
            return Convert(OpenPackage(path), new LucenePackage(FileSystem, _ => FileSystem.OpenFile(path)));
        }

        public LucenePackage Convert(IPackage package)
        {
            if (package is LucenePackage) return (LucenePackage)package;

            var lucenePackage = new LucenePackage(FileSystem);

            return Convert(package, lucenePackage);
        }

        private LucenePackage Convert(IPackage package, LucenePackage lucenePackage)
        {
            Mapper.Map(package, lucenePackage);

            var path = GetPackageFilePath(lucenePackage);
            lucenePackage.Path = path;

            var derivedData = CalculateDerivedData(lucenePackage, path, lucenePackage.GetStream());

            Mapper.Map(derivedData, lucenePackage);

            return lucenePackage;
        }
    }
}