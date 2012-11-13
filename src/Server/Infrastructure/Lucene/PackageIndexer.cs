using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lucene.Net.Index;
using Lucene.Net.Linq;
using Lucene.Net.Search;
using Ninject;

namespace NuGet.Server.Infrastructure.Lucene
{
    public class PackageIndexer : IPackageIndexer, IInitializable, IDisposable
    {
        private readonly object writeLock = new object();

        private volatile bool disposed;
        private volatile IndexingStatus indexingStatus = new IndexingStatus { State = IndexingState.Idle };
        private readonly IList<IPackage> pendingDownloadIncrements = new List<IPackage>();
        private Thread downloadCounterThread;

        [Inject]
        public IFileSystem FileSystem { get; set; }

        [Inject]
        public IndexWriter Writer { get; set; }

        [Inject]
        public LuceneDataProvider Provider { get; set; }

        [Inject]
        public ILucenePackageRepository PackageRepository { get; set; }

        public TimeSpan CommitInterval { get; set; }

        public PackageIndexer()
        {
            CommitInterval = TimeSpan.FromMinutes(1);
        }

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

            UpdateStatus(IndexingState.Scanning);

            // Sync lucene index with filesystem whenever the web app starts.
            BeginSynchronizeIndexWithFileSystem(cb, this);

            downloadCounterThread = new Thread(DownloadIncrementLoop) { Name = "Lucene Package Download Count Updater", IsBackground = true };
            downloadCounterThread.Start();
        }

        public void Dispose()
        {
            disposed = true;
            if (downloadCounterThread == null) return;
            lock (pendingDownloadIncrements)
            {
                Monitor.Pulse(pendingDownloadIncrements);    
            }
            downloadCounterThread.Join();
            downloadCounterThread = null;
        }

        /// <summary>
        /// Gets status of index building activity.
        /// </summary>
        public IndexingStatus GetIndexingStatus()
        {
            return indexingStatus;
        }

        private void UpdateStatus(IndexingState state, int completedPackages = 0, int packagesToIndex = 0, string currentPackagePath = null)
        {
            using (var reader = Writer.GetReader())
            {
                indexingStatus = new IndexingStatus
                {
                    State = state,
                    CompletedPackages = completedPackages,
                    PackagesToIndex = packagesToIndex,
                    CurrentPackagePath = currentPackagePath,
                    TotalPackages = reader.NumDocs(),
                    PendingDeletes = reader.NumDeletedDocs,
                    IsOptimized = reader.IsOptimized(),
                    LastModification = DateTimeUtils.FromJava(reader.IndexCommit.Timestamp)
                };
            }
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
                    throw (Exception) task.Exception ?? new InvalidOperationException("Task faulted but Exception was null.");
                }
            }
        }

        public void Optimize()
        {
            lock (writeLock)
            {
                UpdateStatus(IndexingState.Optimizing);

                Writer.Optimize();

                UpdateStatus(IndexingState.Idle);
            }
        }

        public void SynchronizeIndexWithFileSystem()
        {
            lock (writeLock)
            {
                UpdateStatus(IndexingState.Scanning);

                var session = OpenSession();
                try
                {
                    SynchronizeIndexWithFileSystem(IndexDifferenceCalculator.FindDifferences(FileSystem, session.Query()), session);
                }
                finally
                {
                    UpdateStatus(IndexingState.Idle);
                    session.Dispose();
                }
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

        public void IncrementDownloadCount(IPackage package)
        {
            if (string.IsNullOrWhiteSpace(package.Id))
            {
                throw new InvalidOperationException("Package Id must be specified.");
            }

            if (package.Version == null)
            {
                throw new InvalidOperationException("Package Version must be specified.");
            }

            lock (pendingDownloadIncrements)
            {
                pendingDownloadIncrements.Add(package);
                Monitor.Pulse(pendingDownloadIncrements);
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

            var elapsedTimer = Stopwatch.StartNew();

            for (var i = 0; i < pathsToIndex.Length; i++)
            {
                var path = pathsToIndex[i];
                UpdateStatus(IndexingState.Building, completedPackages: i, packagesToIndex: pathsToIndex.Length, currentPackagePath: path);

                SynchronizePackage(session, path);

                if (elapsedTimer.Elapsed > CommitInterval)
                {
                    UpdateStatus(IndexingState.Commit, completedPackages: i, packagesToIndex: pathsToIndex.Length, currentPackagePath: path);
                    session.Commit();
                    elapsedTimer.Restart();
                }
            }

            UpdateStatus(IndexingState.Commit);
            session.Commit();

            UpdateStatus(IndexingState.Optimizing);
            Writer.Optimize(true);

            Log.Info(string.Format("Lucene index updated: {0} packages added, {1} packages updated, {2} packages removed.", diff.NewPackages.Count(), diff.ModifiedPackages.Count(), deleteQueries.Length));
        }

        private void SynchronizePackage(ISession<LucenePackage> session, string path)
        {
            try
            {
                var package = PackageRepository.LoadFromFileSystem(path);
                AddPackage(package, session);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to index package path: " + path, ex);
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

        private void DownloadIncrementLoop()
        {
            while (!disposed)
            {
                IList<IPackage> copy;

                lock (pendingDownloadIncrements)
                {
                    copy = PendingDownloadIncrements;
                    pendingDownloadIncrements.Clear();

                    if (copy.IsEmpty())
                    {
                        Monitor.Wait(pendingDownloadIncrements, TimeSpan.FromMinutes(1));
                        continue;
                    }
                }

                ApplyPendingDownloadIncrements(copy);
            }
        }

        public IList<IPackage> PendingDownloadIncrements
        {
            get { return new List<IPackage>(pendingDownloadIncrements); }
        }

        public void ApplyPendingDownloadIncrements(IList<IPackage> increments)
        {
            var byId = increments.ToLookup(p => p.Id);

            lock (writeLock)
            {
                UpdateStatus(IndexingState.Commit);
                using (var session = OpenSession())
                {
                    foreach (var grouping in byId)
                    {
                        var packages = from p in session.Query() where p.Id == grouping.Key select p;
                        var byVersion = grouping.ToLookup(p => p.Version);

                        foreach (var lucenePackage in packages)
                        {
                            lucenePackage.DownloadCount += grouping.Count();
                            lucenePackage.VersionDownloadCount += byVersion[lucenePackage.Version].Count();
                        }
                    }
                }
                UpdateStatus(IndexingState.Idle);
            }
        }

        private ISession<LucenePackage> OpenSession()
        {
            return Provider.OpenSession(() => new LucenePackage(FileSystem));
        }
    }
}