using System.Linq;
using Moq;
using NuGet;
using NuGet.Server.Infrastructure.Lucene;
using Xunit;

namespace Server.Test.Lucene
{
    public class LucenePackageRepositoryTests : TestBase
    {
        private readonly Mock<IPackageIndexer> indexer;
        private readonly LucenePackageRepository repository;

        public LucenePackageRepositoryTests()
        {
            indexer = new Mock<IPackageIndexer>();
            repository = new LucenePackageRepository(packagePathResolver.Object, fileSystem.Object)
                             {
                                 Indexer = indexer.Object,
                                 LucenePackages = new EnumerableQuery<LucenePackage>(new LucenePackage[0])
                             };
        }

        [Fact]
        public void IncrementDownloadCount()
        {
            var pkg = MakeSamplePackage("sample", "2.1");
            indexer.Setup(i => i.IncrementDownloadCount(pkg)).Verifiable();

            repository.IncrementDownloadCount(pkg);

            indexer.Verify();
        }

        [Fact]
        public void IncrementDownloadCount_UpdatesMaxDownload()
        {
            var pkg = new DataServicePackage();
            var p1 = MakeSamplePackage("a", "1.0");
            var p2 = MakeSamplePackage("a", "1.0");
            var p3 = MakeSamplePackage("a", "1.0");
            p2.DownloadCount = 91982;
            repository.LucenePackages = new EnumerableQuery<LucenePackage>(new[] {p1, p2, p3});

            repository.IncrementDownloadCount(pkg);

            Assert.Equal(p2.DownloadCount, repository.MaxDownloadCount);
        }

        [Fact]
        public void Initialize_UpdatesMaxDownload()
        {
            var p = MakeSamplePackage("a", "1.0");
            p.DownloadCount = 1234;
            repository.LucenePackages = new EnumerableQuery<LucenePackage>(new[] { p });

            repository.Initialize();

            Assert.Equal(p.DownloadCount, repository.MaxDownloadCount);
        }

        [Fact]
        public void RemovePackage()
        {
            var doomed = MakeSamplePackage("deleteme", "1.0-alpha");
            indexer.Setup(i => i.RemovePackage(doomed)).Verifiable();
            
            repository.RemovePackage(doomed);

            indexer.Verify();
        }

        [Fact]
        public void RemovePackage_UpdatesMaxDownload()
        {
            var doomed = MakeSamplePackage("deleteme", "1.0-alpha");
            var p = MakeSamplePackage("a", "1.0");
            p.DownloadCount = 1234;
            repository.LucenePackages = new EnumerableQuery<LucenePackage>(new[] { p });

            repository.RemovePackage(doomed);

            Assert.Equal(p.DownloadCount, repository.MaxDownloadCount);
        }
    }
}