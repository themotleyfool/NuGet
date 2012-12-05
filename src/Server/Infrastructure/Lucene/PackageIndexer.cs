using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Lucene.Net.Index;
using Lucene.Net.Linq;
using Lucene.Net.Search;
using Ninject;

namespace NuGet.Server.Infrastructure.Lucene
{
    public class PackageIndexer : IPackageIndexer, IInitializable, IDisposable
    {
        public static ILog Log = LogManager.GetLogger<PackageIndexer>();

        private enum UpdateType { Add, Remove, RemoveByPath, Increment }
        private class Update
        {
            private readonly LucenePackage package;
            private readonly UpdateType updateType;
            private readonly TaskCompletionSource<object> signal = new TaskCompletionSource<object>();

            public Update(LucenePackage package, UpdateType updateType)
            {
                this.package = package;
                this.updateType = updateType;
            }

            public LucenePackage Package
            {
                get { return package; }
            }

            public UpdateType UpdateType
            {
                get { return updateType; }
            }

            public Task Task
            {
                get
                {
                    return signal.Task;
                }
            }

            public void SetComplete()
            {
                signal.SetResult(null);
            }
        }

        private readonly IList<IndexingStatus> indexingStatus = new List<IndexingStatus> { new IndexingStatus { State = IndexingState.Idle } };
        private readonly BlockingCollection<Update> pendingUpdates = new BlockingCollection<Update>();
        private Task indexUpdaterTask;

        [Inject]
        public IFileSystem FileSystem { get; set; }

        [Inject]
        public IndexWriter Writer { get; set; }

        [Inject]
        public LuceneDataProvider Provider { get; set; }

        [Inject]
        public ILucenePackageRepository PackageRepository { get; set; }

        public void Initialize()
        {
            Action<Task> cb = task =>
                {
                    if (task.Exception != null)
                    {
                        Log.Error(task.Exception);
                    }
                };

            // Sync lucene index with filesystem whenever the web app starts.
            SynchronizeIndexWithFileSystem().ContinueWith(cb);

            indexUpdaterTask = Task.Factory.StartNew(IndexUpdateLoop, TaskCreationOptions.LongRunning);
        }

        public void Dispose()
        {
            pendingUpdates.CompleteAdding();
            indexUpdaterTask.Wait();
        }

        /// <summary>
        /// Gets status of index building activity.
        /// </summary>
        public IndexingStatus GetIndexingStatus()
        {
            IndexingStatus current;

            lock (indexingStatus)
            {
                current = indexingStatus.Last();
            }

            using (var reader = Writer.GetReader())
            {
                return new IndexingStatus
                {
                    State = current.State,
                    CompletedPackages = current.CompletedPackages,
                    PackagesToIndex = current.PackagesToIndex,
                    CurrentPackagePath = current.CurrentPackagePath,
                    TotalPackages = reader.NumDocs(),
                    PendingDeletes = reader.NumDeletedDocs,
                    IsOptimized = reader.IsOptimized(),
                    LastModification = DateTimeUtils.FromJava(reader.IndexCommit.Timestamp)
                };
            }
        }
        
        public void Optimize()
        {
            using (UpdateStatus(IndexingState.Optimizing))
            {
                Writer.Optimize();
            }
        }

        public Task SynchronizeIndexWithFileSystem()
        {
            IndexDifferences differences = null;
            Action findDifferences = () =>
                {
                    
                    using (UpdateStatus(IndexingState.Scanning))
                    {
                        using (var session = OpenSession())
                        {
                            differences = IndexDifferenceCalculator.FindDifferences(FileSystem, session.Query());
                        }
                    }
                };

            return Task.Run(findDifferences).ContinueWith(task => SynchronizeIndexWithFileSystem(differences), TaskContinuationOptions.NotOnFaulted);
        }

        public Task AddPackage(LucenePackage package)
        {
            var update = new Update(package, UpdateType.Add);
            pendingUpdates.Add(update);
            return update.Task;
        }

        public Task RemovePackage(IPackage package)
        {
            if (!(package is LucenePackage)) throw new ArgumentException("Package of type " + package.GetType() + " not supported.");

            var update = new Update((LucenePackage)package, UpdateType.Remove);
            pendingUpdates.Add(update);
            return update.Task;
        }

        public Task IncrementDownloadCount(IPackage package)
        {
            if (!(package is LucenePackage)) throw new ArgumentException("Package of type " + package.GetType() + " not supported.");

            if (string.IsNullOrWhiteSpace(package.Id))
            {
                throw new InvalidOperationException("Package Id must be specified.");
            }

            if (package.Version == null)
            {
                throw new InvalidOperationException("Package Version must be specified.");
            }

            var update = new Update((LucenePackage) package, UpdateType.Increment);
            pendingUpdates.Add(update);
            return update.Task;
        }

        internal void SynchronizeIndexWithFileSystem(IndexDifferences diff)
        {
            if (diff.IsEmpty) return;

            var tasks = new ConcurrentQueue<Task>();

            Log.Info(string.Format("Updates to process: {0} packages added, {1} packages updated, {2} packages removed.", diff.NewPackages.Count(), diff.ModifiedPackages.Count(), diff.MissingPackages.Count()));

            foreach (var path in diff.MissingPackages)
            {
                var package = new LucenePackage(FileSystem) { Path = path };
                var update = new Update(package, UpdateType.RemoveByPath);
                pendingUpdates.Add(update);
                tasks.Enqueue(update.Task);
            }
            
            var pathsToIndex = diff.NewPackages.Union(diff.ModifiedPackages).OrderBy(p => p).ToArray();

            var i = 0;

            Parallel.ForEach(pathsToIndex, new ParallelOptions() { MaxDegreeOfParallelism = 5 }, (p, s) =>
                {
                    using(UpdateStatus(IndexingState.Building, completedPackages: Interlocked.Increment(ref i), packagesToIndex: pathsToIndex.Length, currentPackagePath: p))
                    {
                        tasks.Enqueue(SynchronizePackage(p));
                    }
                });

            Task.WaitAll(tasks.ToArray());
        }

        private Task SynchronizePackage(string path)
        {
            try
            {
                var package = PackageRepository.LoadFromFileSystem(path);
                return AddPackage(package);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to index package path: " + path, ex);
                return Task.FromResult(ex);
            }
        }
        
        private void IndexUpdateLoop()
        {
            while (!pendingUpdates.IsCompleted)
            {
                var items = pendingUpdates.TakeAvailable(Timeout.InfiniteTimeSpan).ToList();

                if (items.Any())
                {
                    ApplyUpdates(items);
                }
                
                items.ForEach(i => i.SetComplete());
            }
        }

        private void ApplyUpdates(IList<Update> items)
        {
            Log.Trace(m => m("Processing {0} updates.", items.Count()));

            using (var session = OpenSession())
            {
                using (UpdateStatus(IndexingState.Building))
                {

                    var removals =
                        items.Where(i => i.UpdateType == UpdateType.Remove).Select(i => i.Package).ToList();
                    removals.ForEach(pkg => RemovePackageInternal(pkg, session));

                    var removalsByPath =
                        items.Where(i => i.UpdateType == UpdateType.RemoveByPath).Select(i => i.Package.Path).ToList();
                    var deleteQueries = removalsByPath.Select(p => (Query)new TermQuery(new Term("Path", p))).ToArray();
                    session.Delete(deleteQueries);

                    var additions = items.Where(i => i.UpdateType == UpdateType.Add).Select(i => i.Package).ToList();
                    ApplyPendingAdditions(additions, session);

                    var downloadUpdates =
                        items.Where(i => i.UpdateType == UpdateType.Increment).Select(i => i.Package).ToList();
                    ApplyPendingDownloadIncrements(downloadUpdates, session);
                }

                using (UpdateStatus(IndexingState.Commit))
                {
                    session.Commit();
                }
            }
        }

        private void ApplyPendingAdditions(IEnumerable<LucenePackage> additions, ISession<LucenePackage> session)
        {
            foreach (var grouping in additions.GroupBy(pkg => pkg.Id))
            {
                AddPackagesInternal(grouping.Key, grouping.ToList(), session);
            }
        }

        private void AddPackagesInternal(string packageId, IEnumerable<LucenePackage> packages, ISession<LucenePackage> session)
        {
            var currentPackages = (from p in session.Query()
                                   where p.Id == packageId
                                   orderby p.Version descending
                                   select p).ToList();

            var newest = currentPackages.FirstOrDefault();
            var versionDownloadCount = newest != null ? newest.VersionDownloadCount : 0;

            foreach (var package in packages)
            {
                var packageToReplace = currentPackages.Find(p => p.Version == package.Version);

                package.VersionDownloadCount = versionDownloadCount;
                package.DownloadCount = packageToReplace != null ? packageToReplace.DownloadCount : 0;

                currentPackages.Remove(packageToReplace);
                currentPackages.Add(package);

                session.Add(package);
            }

            UpdatePackageVersionFlags(currentPackages.OrderByDescending(p => p.Version));
        }

        private void RemovePackageInternal(LucenePackage package, ISession<LucenePackage> session)
        {
            session.Delete(package);

            var remainingPackages = from p in session.Query()
                                    where p.Id == package.Id
                                    orderby p.Version descending
                                    select p;

            UpdatePackageVersionFlags(remainingPackages);
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

        //public IList<IPackage> PendingDownloadIncrements
        //{
        //    get { return new List<IPackage>(pendingUpdates); }
        //}

        public void ApplyPendingDownloadIncrements(IList<LucenePackage> increments, ISession<LucenePackage> session)
        {
            if (increments.Count == 0) return;

            var byId = increments.ToLookup(p => p.Id);

            foreach (var grouping in byId)
            {
                var packageId = grouping.Key;
                var packages = from p in session.Query() where p.Id == packageId select p;
                var byVersion = grouping.ToLookup(p => p.Version);

                foreach (var lucenePackage in packages)
                {
                    lucenePackage.DownloadCount += grouping.Count();
                    lucenePackage.VersionDownloadCount += byVersion[lucenePackage.Version].Count();
                }
            }
        }

        protected internal virtual ISession<LucenePackage> OpenSession()
        {
            return Provider.OpenSession(() => new LucenePackage(FileSystem));
        }

        private IDisposable UpdateStatus(IndexingState state, int completedPackages = 0, int packagesToIndex = 0, string currentPackagePath = null)
        {
            var status = new IndexingStatus
                {
                    State = state,
                    CompletedPackages = completedPackages,
                    PackagesToIndex = packagesToIndex,
                    CurrentPackagePath = currentPackagePath
                };

            lock (indexingStatus)
            {
                indexingStatus.Add(status);
            }

            return new DisposableAction(() =>
                {
                    lock (indexingStatus)
                    {
                        indexingStatus.Remove(status);
                    }
                });
        }
    }
}