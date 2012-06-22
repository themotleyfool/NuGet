using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoMapper;
using Lucene.Net.Linq;
using Ninject;
using NuGet.Server.DataServices;
using LuceneDirectory = Lucene.Net.Store.Directory;

namespace NuGet.Server.Infrastructure.Lucene
{
    public class LucenePackageRepository : ServerPackageRepository, ILucenePackageLoader, IInitializable
    {
        static LucenePackageRepository()
        {
            Mapper.CreateMap<IPackage, LucenePackage>();
            Mapper.CreateMap<DerivedPackageData, LucenePackage>();
            Mapper.CreateMap<LucenePackage, DerivedPackageData>();
        }

        [Inject]
        public IQueryable<LucenePackage> LucenePackages { get; set; }

        [Inject]
        public IPackageIndexer Indexer { get; set; }

        [Inject]
        public IPackageFileSystemWatcher FileSystemWatcher { get; set; }

        private volatile int maxDownloadCount;
        public int MaxDownloadCount { get { return maxDownloadCount; } }

        public LucenePackageRepository(IPackagePathResolver packageResolver, IFileSystem fileSystem)
            : base(packageResolver, fileSystem)
        {
        }

        public void Initialize()
        {
            UpdateMaxDownloadCount();
        }

        public override void AddPackage(IPackage package)
        {
            base.AddPackage(package);

            // Tell the watcher to index the package now that it's done being written.
            FileSystemWatcher.EndQuietTime(GetPackageFilePath(package));
        }

        public override void IncrementDownloadCount(IPackage package)
        {
            Indexer.IncrementDownloadCount(package);
            UpdateMaxDownloadCount();
        }

        public void UpdateMaxDownloadCount()
        {
            if (LucenePackages.Any())
            {
                maxDownloadCount = LucenePackages.Max(p => p.DownloadCount);
            }
            else
            {
                maxDownloadCount = 0;
            }
        }

        public override IQueryable<IPackage> GetPackages()
        {
            // cast is necessary due to https://www.re-motion.org/jira/browse/RM-4482
            return LucenePackages.Select(p => (IPackage)p);
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

            // cast is necessary due to https://www.re-motion.org/jira/browse/RM-4482
            return packages.Select(p => (IPackage)p);
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
            return Convert(OpenPackage(path));
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