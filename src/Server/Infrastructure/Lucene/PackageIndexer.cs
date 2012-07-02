using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Index;
using Lucene.Net.Linq;
using Lucene.Net.Search;
using Ninject;

namespace NuGet.Server.Infrastructure.Lucene
{
    public class PackageIndexer : IPackageIndexer, IInitializable
    {
        private readonly object writeLock = new object();

        private volatile IndexingStatus indexingStatus = new IndexingStatus { State = IndexingState.Idle };

        [Inject]
        public IFileSystem FileSystem { get; set; }

        [Inject]
        public IndexWriter Writer { get; set; }

        [Inject]
        public LuceneDataProvider Provider { get; set; }

        [Inject]
        public ILucenePackageLoader PackageLoader { get; set; }

        public void Initialize()
        {
            AsyncCallback cb = ar =>
                {
                    try
                    {
                        EndSynchronizeIndexWithFileSystem(ar);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                };

            // Sync lucene index with filesystem whenever the web app starts.
            BeginSynchronizeIndexWithFileSystem(cb, this);
        }

        /// <summary>
        /// Gets status of index building activity.
        /// </summary>
        public IndexingStatus GetIndexingStatus()
        {
            return indexingStatus;
        }

        public IAsyncResult BeginSynchronizeIndexWithFileSystem(AsyncCallback callback, object clientState)
        {
            var task = new Task(state => SynchronizeIndexWithFileSystem(), clientState, TaskCreationOptions.LongRunning);

            if (callback != null)
            {
                task.ContinueWith(t => callback(t));
            }

            task.Start();

            return task;

        }

        public void EndSynchronizeIndexWithFileSystem(IAsyncResult ar)
        {
            var task = (Task)ar;
            using (task)
            {
                if (task.IsFaulted)
                {
                    throw task.Exception;
                }
            }
        }

        public void SynchronizeIndexWithFileSystem()
        {
            lock (writeLock)
            {
                indexingStatus = new IndexingStatus { State = IndexingState.Scanning };

                var session = OpenSession();
                try
                {
                    SynchronizeIndexWithFileSystem(IndexDifferenceCalculator.FindDifferences(FileSystem, session.Query()), session);
                }
                finally
                {
                    indexingStatus = new IndexingStatus { State = IndexingState.Idle };
                    session.Dispose();
                }
            }
        }

        public void SynchronizeIndexWithFileSystem(IndexDifferences diff, ISession<LucenePackage> session)
        {
            if (diff.IsEmpty) return;

            var deleteQueries = diff.MissingPackages.Select(p => (Query)new TermQuery(new Term("Path", p))).ToArray();

            if (deleteQueries.Any())
            {
                session.Delete(deleteQueries);
            }

            var pathsToIndex = diff.NewPackages.Union(diff.ModifiedPackages).OrderBy(p => p).ToArray();

            for (var i = 0; i < pathsToIndex.Length; i++)
            {
                var path = pathsToIndex[i];
                indexingStatus = new IndexingStatus { State = IndexingState.Building, CompletedPackages = i, PackagesToIndex = pathsToIndex.Length, CurrentPackagePath = path };

                SynchronizePackage(session, path);
            }

            session.Commit();

            indexingStatus = new IndexingStatus { State = IndexingState.Optimizing };
            Writer.Optimize(true);

            Log.Info(string.Format("Lucene index updated: {0} packages added, {1} packages updated, {2} packages removed.", diff.NewPackages.Count(), diff.ModifiedPackages.Count(), deleteQueries.Length));
        }

        private void SynchronizePackage(ISession<LucenePackage> session, string path)
        {
            try
            {
                var package = PackageLoader.LoadFromFileSystem(path);
                AddPackage(package, session);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to index package path: " + path, ex);
            }
        }

        public void AddPackage(LucenePackage package)
        {
            lock (writeLock)
            {
                using (var session = OpenSession())
                {
                    AddPackage(package, session);
                }
            }
        }

        private void AddPackage(LucenePackage package, ISession<LucenePackage> session)
        {
            var currentPackages = (from p in session.Query()
                                   where p.Id == package.Id
                                   orderby p.Version descending
                                   select p).ToList();

            var newest = currentPackages.FirstOrDefault();

            if (newest == null)
            {
                package.VersionDownloadCount = 0;
                package.DownloadCount = 0;
            }
            else
            {
                package.DownloadCount = newest.DownloadCount;
                package.VersionDownloadCount = 0;

                if (newest.Version == package.Version)
                {
                    package.VersionDownloadCount = newest.VersionDownloadCount;
                }
            }

            currentPackages.RemoveAll(p => p.Version == package.Version);
            currentPackages.Add(package);

            session.Add(package);

            UpdatePackageVersionFlags(currentPackages.OrderByDescending(p => p.Version));
        }

        public void RemovePackage(IPackage package)
        {
            if (!(package is LucenePackage)) throw new ArgumentException("Package of type " + package.GetType() + " not supported.");

            var lucenePackage = (LucenePackage)package;

            lock (writeLock)
            {
                using (var session = OpenSession())
                {
                    var remainingPackages = from p in session.Query()
                                            where p.Id == package.Id && p.Version != package.Version
                                            orderby p.Version descending
                                            select p;

                    session.Delete(lucenePackage);

                    UpdatePackageVersionFlags(remainingPackages);
                }
            }
        }

        private void UpdatePackageVersionFlags(IEnumerable<LucenePackage> packages)
        {
            var first = true;
            foreach (var p in packages)
            {
                p.IsLatestVersion = first;
                p.IsAbsoluteLatestVersion = first;

                if (first)
                {
                    first = false;
                }
            }
        }

        public void IncrementDownloadCount(IPackage package)
        {
            lock (writeLock)
            {
                using (var session = OpenSession())
                {
                    var packages = from p in session.Query() where p.Id == package.Id select p;

                    foreach (var p in packages)
                    {
                        p.DownloadCount++;
                        if (p.Version == package.Version)
                        {
                            p.VersionDownloadCount++;
                        }
                    }
                }
            }
        }

        private ISession<LucenePackage> OpenSession()
        {
            return Provider.OpenSession(() => new LucenePackage(FileSystem));
        }
    }
}