using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        public async Task PackageModified()
        {
            SetupPackageIsModified("Sample.1.0.nupkg");
            
            await watcher.OnPackageModified(@".\Sample.1.0.nupkg");

            loader.Verify();
            indexer.Verify();
        }

        [Fact]
        public async Task PackageModified_HandlesException()
        {
            var exception = new Exception("mock error");

            loader.Setup(ld => ld.LoadFromIndex(@".\Sample.1.0.nupkg")).Throws(exception);
            log.Setup(l => l.Error(exception));

            await watcher.OnPackageModified(@".\Sample.1.0.nupkg");

            loader.Verify();
            indexer.Verify();
        }

        [Fact]
        public async Task PackageDeleted()
        {
            SetupDeletePackage("Sample.1.0.nupkg");

            await watcher.OnPackageDeleted(@".\Sample.1.0.nupkg");

            loader.Verify();
            indexer.Verify();
        }

        [Fact]
        public async Task PackageDeleted_HandlesException()
        {
            var exception = new Exception("mock error");
            var lucenePackage = new LucenePackage(fileSystem.Object);

            loader.Setup(ld => ld.LoadFromIndex(@".\Sample.1.0.nupkg")).Returns(lucenePackage);
            indexer.Setup(idx => idx.RemovePackage(lucenePackage)).Throws(exception);
            log.Setup(l => l.Error(exception));

            await watcher.OnPackageDeleted("Sample.1.0.nupkg");

            loader.Verify();
            indexer.Verify();
        }

        [Fact]
        public async Task PackageDeleted_IgnoresPackageMissingFromIndex()
        {
            loader.Setup(ld => ld.LoadFromIndex(@"Sample.1.0.nupkg")).Returns((LucenePackage)null);

            await watcher.OnPackageDeleted("Sample.1.0.nupkg");

            loader.Verify();
            indexer.Verify();
        }

        [Fact]
        public async Task PackageRenamed()
        {
            SetupDeletePackage("tmp.nupkg");
            SetupPackageIsModified("Sample.1.0.nupkg");

            await watcher.OnPackageRenamed(@".\tmp.nupkg", @".\Sample.1.0.nupkg");

            loader.Verify();
            indexer.Verify();
        }

        [Fact]
        public async Task PackageRenamed_IgnoresNonPackageExtension()
        {
            SetupDeletePackage("Sample.1.0.nupkg");
            
            await watcher.OnPackageRenamed(@".\Sample.1.0.nupkg", @".\IgnoreMe.tmp");
            
            loader.Verify();
            indexer.Verify();
        }

        [Fact]
        public void SynchronizeAfterDirectoryCreated()
        {
            const string dir = @"c:\sample\dir";

            fileSystem.Setup(fs => fs.GetFiles(dir, "*.nupkg", true)).Returns(new[] { "Sample.1.0.nupkg" });
            indexer.Setup(idx => idx.SynchronizeIndexWithFileSystem());

            watcher.OnDirectoryMoved(Path.GetDirectoryName(dir));

            fileSystem.Verify();
            indexer.Verify();
        }

        [Fact]
        public void DirectoryCreatedIgnoreEmptyDir()
        {
            const string dir = @"c:\sample\dir";

            fileSystem.Setup(fs => fs.GetFiles(dir, "*.nupkg", true)).Returns(new string[0]);

            watcher.OnDirectoryMoved(Path.GetDirectoryName(dir));

            fileSystem.Verify();
            indexer.Verify();
        }

        private void SetupPackageIsModified(string filename)
        {
            var lucenePackage = new LucenePackage(fileSystem.Object);
            loader.Setup(ld => ld.LoadFromIndex(@".\" + filename)).Returns((LucenePackage)null).Verifiable();
            loader.Setup(ld => ld.LoadFromFileSystem(@".\" + filename)).Returns(lucenePackage).Verifiable();
            indexer.Setup(idx => idx.AddPackage(lucenePackage)).Returns(Task.FromResult<object>(null)).Verifiable();
        }

        private void SetupPackageIsNotModified(string filename)
        {
            var lucenePackage = new LucenePackage(fileSystem.Object) { Published = null };

            loader.Setup(ld => ld.LoadFromIndex(@".\" + filename)).Returns(lucenePackage).Verifiable();
            loader.Setup(ld => ld.LoadFromFileSystem(@".\" + filename)).Returns(lucenePackage).Verifiable();
            indexer.Setup(idx => idx.AddPackage(lucenePackage)).Verifiable();
        }

        private void SetupDeletePackage(string filename)
        {
            var lucenePackage = new LucenePackage(fileSystem.Object);
            loader.Setup(ld => ld.LoadFromIndex(@".\" + filename)).Returns(lucenePackage).Verifiable();
            indexer.Setup(idx => idx.RemovePackage(lucenePackage)).Returns(Task.FromResult<object>(null)).Verifiable();
        }
    }

}