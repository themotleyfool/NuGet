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
                                 LucenePackages = new EnumerableQuery<LucenePackage>(new LucenePackage[0]),
                                 LuceneDataProvider = provider
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
        public void Initialize_UpdatesMaxDownload()
        {
            var p = MakeSamplePackage("a", "1.0");
            p.DownloadCount = 1234;
            repository.LucenePackages = new EnumerableQuery<LucenePackage>(new[] { p });

            repository.Initialize();

            Assert.Equal(p.DownloadCount, repository.MaxDownloadCount);
        }
    }
}