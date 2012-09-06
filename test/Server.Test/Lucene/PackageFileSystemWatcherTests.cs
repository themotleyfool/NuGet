using System;
using System.IO;
using Common.Logging;
using Moq;
using NuGet.Server.Infrastructure.Lucene;
using Xunit;

namespace Server.Test.Lucene
{
    public class PackageFileSystemWatcherTests : TestBase
    {
        private readonly PackageFileSystemWatcher watcher;
        private readonly Mock<IPackageIndexer> indexer;
        private Mock<ILog> log;

        public PackageFileSystemWatcherTests()
        {
            indexer = new Mock<IPackageIndexer>(MockBehavior.Strict);

            watcher = new PackageFileSystemWatcher
                          {
                              FileSystem = fileSystem.Object,
                              Indexer = indexer.Object,
                              PackageRepository = loader.Object,
                              QuietTime = TimeSpan.Zero
                          };

            log = new Mock<ILog>();
            PackageFileSystemWatcher.Log = log.Object;
        }

        [Fact]
        public void PackageModified()
        {
            SetupAddPackage("Sample.1.0.nupkg");

            watcher.OnPackageModified(this, new FileSystemEventArgs(WatcherChangeTypes.Changed, ".", "Sample.1.0.nupkg"));

            loader.Verify();
            indexer.Verify();
        }

        [Fact]
        public void PackageModified_HandlesException()
        {
            var exception = new Exception("mock error");

            loader.Setup(ld => ld.LoadFromFileSystem(@".\Sample.1.0.nupkg")).Throws(exception);
            log.Setup(l => l.Error(exception));

            watcher.OnPackageModified(this, new FileSystemEventArgs(WatcherChangeTypes.Changed, ".", "Sample.1.0.nupkg"));

            loader.Verify();
            indexer.Verify();
        }

        [Fact]
        public void PackageDeleted()
        {
            SetupDeletePackage("Sample.1.0.nupkg");

            watcher.OnPackageDeleted(this, new FileSystemEventArgs(WatcherChangeTypes.Deleted, ".", "Sample.1.0.nupkg"));

            loader.Verify();
            indexer.Verify();
        }

        [Fact]
        public void PackageDeleted_HandlesException()
        {
            var exception = new Exception("mock error");
            var lucenePackage = new LucenePackage(fileSystem.Object);

            loader.Setup(ld => ld.LoadFromIndex(@".\Sample.1.0.nupkg")).Returns(lucenePackage);
            indexer.Setup(idx => idx.RemovePackage(lucenePackage)).Throws(exception);
            log.Setup(l => l.Error(exception));

            watcher.OnPackageDeleted(this, new FileSystemEventArgs(WatcherChangeTypes.Changed, ".", "Sample.1.0.nupkg"));

            loader.Verify();
            indexer.Verify();
        }

        [Fact]
        public void PackageDeleted_IgnoresPackageMissingFromIndex()
        {
            loader.Setup(ld => ld.LoadFromIndex(@".\Sample.1.0.nupkg")).Returns((LucenePackage)null);

            watcher.OnPackageDeleted(this, new FileSystemEventArgs(WatcherChangeTypes.Deleted, ".", "Sample.1.0.nupkg"));

            loader.Verify();
            indexer.Verify();
        }

        [Fact]
        public void PackageRenamed()
        {
            SetupDeletePackage("tmp.nupkg");
            SetupAddPackage("Sample.1.0.nupkg");

            watcher.OnPackageRenamed(this, new RenamedEventArgs(WatcherChangeTypes.Renamed, ".", "Sample.1.0.nupkg", "tmp.nupkg"));

            loader.Verify();
            indexer.Verify();
        }

        [Fact]
        public void SynchronizeAfterDirectoryCreated()
        {
            const string dir = @"c:\sample\dir";

            fileSystem.Setup(fs => fs.GetFiles(dir, "*.nupkg", true)).Returns(new[] { "Sample.1.0.nupkg" });
            indexer.Setup(idx => idx.SynchronizeIndexWithFileSystem());

            watcher.OnDirectoryMoved(this, new FileSystemEventArgs(WatcherChangeTypes.Created, Path.GetDirectoryName(dir), Path.GetFileName(dir)));

            fileSystem.Verify();
            indexer.Verify();
        }

        [Fact]
        public void DirectoryCreatedIgnoreEmptyDir()
        {
            const string dir = @"c:\sample\dir";

            fileSystem.Setup(fs => fs.GetFiles(dir, "*.nupkg", true)).Returns(new string[0]);

            watcher.OnDirectoryMoved(this, new FileSystemEventArgs(WatcherChangeTypes.Created, Path.GetDirectoryName(dir), Path.GetFileName(dir)));

            fileSystem.Verify();
            indexer.Verify();
        }

        private void SetupAddPackage(string filename)
        {
            var lucenePackage = new LucenePackage(fileSystem.Object);
            loader.Setup(ld => ld.LoadFromFileSystem(@".\" + filename)).Returns(lucenePackage);
            indexer.Setup(idx => idx.AddPackage(lucenePackage));
        }

        private void SetupDeletePackage(string filename)
        {
            var lucenePackage = new LucenePackage(fileSystem.Object);
            loader.Setup(ld => ld.LoadFromIndex(@".\" + filename)).Returns(lucenePackage);
            indexer.Setup(idx => idx.RemovePackage(lucenePackage));
        }
    }

}