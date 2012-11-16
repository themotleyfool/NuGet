using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Common.Logging;
using Ninject;

namespace NuGet.Server.Infrastructure.Lucene
{
    public interface IPackageFileSystemWatcher
    {
        /// <summary>
        /// Tell watcher that a package contents have been completely written to disk
        /// and it is safe to index the package. If the package is already being indexed
        /// on another thread, this method will block until the operation completes.
        /// </summary>
        void EndQuietTime(string path);
    }

    public class PackageFileSystemWatcher : IPackageFileSystemWatcher, IInitializable, IDisposable
    {
        public const string FullReindexTimerKey = "**PackageFileSystemWatcher.Reindex**";

        public static ILog Log = LogManager.GetLogger<PackageFileSystemWatcher>();

        private readonly TimerHelper timerHelper;
        private FileSystemWatcher fileWatcher;
        private FileSystemWatcher dirWatcher;
        private readonly IDictionary<string, EventWaitHandle> indexMonitors = new Dictionary<string, EventWaitHandle>();

        [Inject]
        public IFileSystem FileSystem { get; set; }

        [Inject]
        public ILucenePackageRepository PackageRepository { get; set; }

        [Inject]
        public IPackageIndexer Indexer { get; set; }

        /// <summary>
        /// Sets the amount of time to wait after receiving a <c cref="FileSystemWatcher.Changed">Changed</c>
        /// event before attempting to index a package. This timeout is meant to avoid trying to read a package
        /// while it is still being built or copied into place.
        /// </summary>
        public TimeSpan QuietTime { get; set; }

        public PackageFileSystemWatcher()
        {
            timerHelper = new TimerHelper();
            QuietTime = TimeSpan.FromSeconds(3);
        }

        public void Initialize()
        {
            fileWatcher = new FileSystemWatcher(FileSystem.Root, "*.nupkg");

            fileWatcher.Changed += OnPackageModified;
            fileWatcher.Deleted += OnPackageDeleted;
            fileWatcher.Renamed += OnPackageRenamed;
            fileWatcher.Created += OnPackageModified;

            fileWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite;
            fileWatcher.IncludeSubdirectories = true;
            fileWatcher.EnableRaisingEvents = true;

            dirWatcher = new FileSystemWatcher(FileSystem.Root);

            dirWatcher.Renamed += OnDirectoryMoved;
            dirWatcher.Created += OnDirectoryMoved;
            dirWatcher.NotifyFilter = NotifyFilters.DirectoryName;
            dirWatcher.IncludeSubdirectories = true;
            dirWatcher.EnableRaisingEvents = true;
        }

        public void OnDirectoryMoved(object sender, FileSystemEventArgs e)
        {
            if (FileSystem.GetFiles(e.FullPath, "*.nupkg", true).Any())
            {
                timerHelper.ResetTimer(FullReindexTimerKey, SynchronizeLater, null, QuietTime);
            }
        }

        private void SynchronizeLater(object state)
        {
            try
            {
                Indexer.SynchronizeIndexWithFileSystem();
            }
            catch (Exception ex)
            {
                Log.Error(m => m("Unhandled exception synchronizing Lucene index with packages on file system.", ex));
            }
        }

        public void Dispose()
        {
            fileWatcher.Dispose();
            dirWatcher.Dispose();
            timerHelper.Dispose();
        }

        public void OnPackageModified(object sender, FileSystemEventArgs e)
        {
            timerHelper.ResetTimer(e.FullPath, AddToIndexAfterQuietTime, e, QuietTime);
        }

        public void EndQuietTime(string path)
        {
            var fullPath = Path.Combine(FileSystem.Root, path);

            if (!timerHelper.CancelTimer(fullPath))
            {
                EventWaitHandle signal;

                lock(indexMonitors)
                {
                    if (!indexMonitors.TryGetValue(fullPath, out signal))
                    {
                        Log.Trace(m => m("No monitors found for path {0}; perhaps no FileSystemWatcher events fired yet.", fullPath));
                    }
                }

                if (signal != null)
                {
                    Log.Trace(m => m("Waiting for asynchronous indexing to complete: " + fullPath));
                    signal.WaitOne();
                    return;
                }
            }

            Log.Trace(m => m("Indexing package synchronously: " + fullPath));
            AddToIndex(fullPath);

            // Cancel any events that came in while we were indexing.
            timerHelper.CancelTimer(fullPath);
        }

        private void AddToIndexAfterQuietTime(object state)
        {
            var e = (FileSystemEventArgs)state;
            var fullPath = e.FullPath;
            EventWaitHandle signal;

            Log.Info(m => m("Quiet time elapsed after file activity on path {0}; Adding package to index.", fullPath));

            lock(indexMonitors)
            {
                if (!indexMonitors.TryGetValue(fullPath, out signal))
                {
                    signal = new ManualResetEvent(false);
                    indexMonitors.Add(fullPath, signal);
                }
            }

            AddToIndex(fullPath);

            lock(indexMonitors)
            {
                indexMonitors.Remove(fullPath);
            }

            signal.Set();
        }

        public void OnPackageRenamed(object sender, RenamedEventArgs e)
        {
            Log.Info(m => m("Package path {0} renamed to {1}.", e.OldFullPath, e.FullPath));
            RemoveFromIndex(e.OldFullPath);
            AddToIndex(e.FullPath);
        }

        public void OnPackageDeleted(object sender, FileSystemEventArgs e)
        {
            Log.Info(m => m("Package path {0} deleted.", e.FullPath));

            RemoveFromIndex(e.FullPath);
        }

        public void AddToIndex(string fullPath)
        {
            try
            {
                var package = PackageRepository.LoadFromFileSystem(fullPath);
                Indexer.AddPackage(package);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        public void RemoveFromIndex(string fullPath)
        {
            try
            {
                var package = PackageRepository.LoadFromIndex(fullPath);
                if (package == null)
                {
                    return;
                }
                Indexer.RemovePackage(package);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

        }
    }
}