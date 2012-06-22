using System;
using System.IO;
using Ninject;

namespace NuGet.Server.Infrastructure.Lucene
{
    public interface IPackageFileSystemWatcher
    {
        void EndQuietTime(string path);
    }

    public class PackageFileSystemWatcher : IPackageFileSystemWatcher, IInitializable, IDisposable
    {
        public const string FullReindexTimerKey = "**PackageFileSystemWatcher.Reindex**";

        public Action<Exception> LogError = Log.Error;

        private TimerHelper timerHelper;
        private FileSystemWatcher fileWatcher;
        private FileSystemWatcher dirWatcher;

        [Inject]
        public IFileSystem FileSystem { get; set; }

        [Inject]
        public ILucenePackageLoader PackageLoader { get; set; }

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
            Indexer.SynchronizeIndexWithFileSystem();
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

            if (timerHelper.CancelTimer(fullPath))
            {
                AddToIndex(fullPath);
            }
        }

        private void AddToIndexAfterQuietTime(object state)
        {
            var e = (FileSystemEventArgs)state;
            AddToIndex(e.FullPath);
        }

        public void OnPackageRenamed(object sender, RenamedEventArgs e)
        {
            RemoveFromIndex(e.OldFullPath);
            AddToIndex(e.FullPath);
        }

        public void OnPackageDeleted(object sender, FileSystemEventArgs e)
        {
            RemoveFromIndex(e.FullPath);
        }

        public void AddToIndex(string fullPath)
        {
            try
            {
                var package = PackageLoader.LoadFromFileSystem(fullPath);
                Indexer.AddPackage(package);
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        public void RemoveFromIndex(string fullPath)
        {
            try
            {
                var package = PackageLoader.LoadFromIndex(fullPath);
                if (package == null)
                {
                    return;
                }
                Indexer.RemovePackage(package);
            }
            catch (Exception ex)
            {
                LogError(ex);
            }

        }
    }
}