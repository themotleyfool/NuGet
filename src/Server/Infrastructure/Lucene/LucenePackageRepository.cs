using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoMapper;
using Lucene.Net.Index;
using Lucene.Net.Linq;
using Lucene.Net.Search;
using Lucene.Net.Store;
using NuGet.Server.DataServices;
using Directory = Lucene.Net.Store.Directory;

namespace NuGet.Server.Infrastructure.Lucene
{
    public class LucenePackageRepository : ServerPackageRepository
    {
        private readonly LuceneDataProvider _provider;
        private readonly IndexWriter _writer;
        private readonly object writeLock = new object();

        static LucenePackageRepository()
        {
            Mapper.CreateMap<IPackage, LucenePackage>();
            Mapper.CreateMap<DerivedPackageData, LucenePackage>();
            Mapper.CreateMap<LucenePackage, DerivedPackageData>();

            global::Lucene.Net.Linq.Util.Log.TraceEnabled = true;
        }

        public LucenePackageRepository(string packagePath, string luceneIndexPath)
            : this(new OneFolderPerPackageIdPathResolver(packagePath), new PhysicalFileSystem(packagePath), OpenLuceneDirectory(luceneIndexPath))
        {
        }

        public LucenePackageRepository(IPackagePathResolver packageResolver, IFileSystem fileSystem, Directory indexDirectory)
            : base(packageResolver, fileSystem)
        {
            var analyzer = new PackageAnalyzer();

            var create = !indexDirectory.ListAll().Any();
            _writer = new IndexWriter(indexDirectory, analyzer, create, IndexWriter.MaxFieldLength.UNLIMITED);

            _provider = new LuceneDataProvider(indexDirectory, analyzer, PackageAnalyzer.IndexVersion, _writer);
        }

        private static Directory OpenLuceneDirectory(string luceneIndexPath)
        {
            var directoryInfo = new DirectoryInfo(luceneIndexPath);
            return FSDirectory.Open(directoryInfo, new NativeFSLockFactory(directoryInfo));
        }

        public override void AddPackage(IPackage package)
        {
            var contents = package.GetStream().ReadAllBytes();

            AddPackage(package, contents);
        }

        public void AddPackage(IPackage package, byte[] contents)
        {
            var lucenePackage = new LucenePackage(FileSystem, p => new MemoryStream(contents));

            Mapper.Map(package, lucenePackage);

            var currentPackages = (from p in LucenePackages
                                   where p.Id == lucenePackage.Id
                                   orderby p.Version descending
                                   select p).ToList();

            var newest = currentPackages.FirstOrDefault();

            lucenePackage.VersionDownloadCount = 0;
            lucenePackage.DownloadCount = newest != null ? newest.DownloadCount : 0;

            currentPackages.RemoveAll(p => p.Version == lucenePackage.Version);
            currentPackages.Add(lucenePackage);

            base.AddPackage(lucenePackage);

            lock (writeLock)
            {
                using (var session = _provider.OpenSession<LucenePackage>())
                {
                    AddPackageToIndex(lucenePackage, CalculateDerivedData, session);

                    session.Commit();

                    UpdatePackageVersionFlags(currentPackages.OrderByDescending(p => p.Version), session);

                    session.Commit();
                }
            }
        }

        private void AddPackageToIndex(LucenePackage package, Func<IPackage, string, Stream, DerivedPackageData> getMetadata, ISession<LucenePackage> session)
        {
            var path = GetPackageFilePath(package);

            var derivedPackageData = getMetadata(package, path, package.GetStream());

            // Map dervied data onto package
            Mapper.Map(derivedPackageData, package);

            package.Published = DateTimeOffset.UtcNow;

            lock (writeLock)
            {
                // delete previous package from index, if present.
                session.Delete(new TermQuery(new Term("Path", path)));
                session.Add(package);
            }
        }

        public override void RemovePackage(IPackage package)
        {
            if (!(package is LucenePackage)) throw new ArgumentException("Package of type " + package.GetType() + " not supported.");

            var lucenePackage = (LucenePackage)package;

            base.RemovePackage(lucenePackage);

            lock (writeLock)
            {
                using (var session = _provider.OpenSession<LucenePackage>())
                {
                    session.Delete(new TermQuery(new Term("Path", lucenePackage.Path)));

                    session.Commit();

                    //TODO: verify this excludes just deleted package:
                    var remainingPackages = from p in LucenePackages
                                           where p.Id == package.Id
                                           orderby p.Version descending
                                           select p;

                    UpdatePackageVersionFlags(remainingPackages, session);

                    session.Commit();
                }
            }
        }

        private void UpdatePackageVersionFlags(IEnumerable<LucenePackage> packages, ISession<LucenePackage> session)
        {
            var first = true;
            foreach (var p in packages)
            {
                if (first)
                {
                    first = false;
                    p.IsLatestVersion = true;
                    p.IsAbsoluteLatestVersion = true;
                }
                else
                {
                    if (!p.IsLatestVersion && !p.IsAbsoluteLatestVersion)
                    {
                        continue;
                    }
                    p.IsLatestVersion = false;
                    p.IsAbsoluteLatestVersion = false;
                }

                session.Delete(new TermQuery(new Term("Path", p.Path)));
                session.Add(p);
            }
        }

        public override void IncrementDownloadCount(IPackage package)
        {
            if (package == null) throw new ArgumentNullException("package");

            lock (writeLock)
            {
                var packages = from p in LucenePackages where p.Id == package.Id select p;

                using (var session = _provider.OpenSession<LucenePackage>())
                {
                    foreach (var p in packages)
                    {
                        p.DownloadCount++;
                        if (p.Version == package.Version)
                        {
                            p.VersionDownloadCount++;
                        }
                        session.Delete(new TermQuery(new Term("Path", p.Path)));
                        session.Add(p);
                    }

                    session.Commit();
                }
            }
        }

        public override IQueryable<IPackage> GetPackages()
        {
            return LucenePackages;
        }

        public override IPackage FindPackage(string packageId, SemanticVersion version)
        {
            var packages = LucenePackages;
            // TODO: version may be a range like {1.0.1} or (1.0,2.0]... how to do range query?
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
                packages = packages.Where(p => p.Text == searchTerm);
            }

            if (!allowPrereleaseVersions)
            {
                packages = packages.Where(p => !p.IsPrerelease);
            }

            return packages.OrderBy(p => p.Score());
        }

        public void ReIndex()
        {
            var packages = LucenePackages.ToList();

            lock (writeLock)
            {
                using (var session = _provider.OpenSession<LucenePackage>())
                {
                    session.DeleteAll();
                    foreach (var p in packages)
                    {
                        if (!p.Published.HasValue)
                        {
                            p.Published = p.Created;
                        }
                        session.Add(p);
                    }

                    session.Commit();
                }

                _writer.Optimize(true);
            }
        }

        public override Package GetMetadataPackage(IPackage package)
        {
            if (!(package is LucenePackage)) throw new ArgumentException("Package of type " + package.GetType() + " not supported.");

            var lucenePackage = (LucenePackage)package;

            var derived = Mapper.Map<LucenePackage, DerivedPackageData>(lucenePackage);

            return new Package(package, derived);
        }

        public IQueryable<LucenePackage> LucenePackages
        {
            get { return _provider.AsQueryable(() => new LucenePackage(FileSystem)); }
        }

        public LuceneDataProvider Provider
        {
            get { return _provider; }
        }
    }
}