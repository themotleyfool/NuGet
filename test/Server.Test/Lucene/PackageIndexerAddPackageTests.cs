using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Server.Test.Lucene
{
    public class PackageIndexerAddPackageTests : PackageIndexerTestBase
    {
        public PackageIndexerAddPackageTests()
        {
            indexer.Initialize();
        }

        [Fact]
        public async void AddPackage()
        {
            await indexer.AddPackage(MakeSamplePackage("Sample.Package", "1.0"));

            Assert.Equal(1, datasource.Count());
        }

        [Fact]
        public async void AddPackage_SetIsLatestVersion()
        {
            await indexer.AddPackage(MakeSamplePackage("Sample.Package", "1.0"));

            Assert.True(datasource.First().IsLatestVersion, "IsLatestVersion");
        }

        [Fact]
        public void AddPackage_MultipleVersions_UnsetIsLatestVersion()
        {
            var t1 = indexer.AddPackage(MakeSamplePackage("Sample.Package", "1.0"));
            var t2 = indexer.AddPackage(MakeSamplePackage("Sample.Package", "1.1"));

            Task.WaitAll(t1, t2);

            var packages = datasource.OrderBy(p => p.Version).ToArray();
            Assert.False(packages.First().IsLatestVersion, "older.IsLatestVersion");
            Assert.True(packages.Last().IsLatestVersion, "newer.IsLatestVersion");
        }

        [Fact]
        public async void AddPackage_Replaces()
        {
            InsertPackage("Sample.Package", "1.0");

            await indexer.AddPackage(MakeSamplePackage("Sample.Package", "1.0"));

            Assert.Equal(1, datasource.Count());
        }

        [Fact]
        public async void AddPackage_ReplacePreservesVersionDownloadCount()
        {
            const int versionDownloadCount = 199;
            var package = MakeSamplePackage("Sample.Package", "1.0");
            package.VersionDownloadCount = versionDownloadCount;
            InsertPackage(package);

            await indexer.AddPackage(package);

            Assert.Equal(versionDownloadCount, datasource.First().VersionDownloadCount);
        }

        [Fact]
        public async void AddPackage_ReplacePreservesDownloadCount()
        {
            const int downloadCount = 23999;
            var package = MakeSamplePackage("Sample.Package", "1.0");
            package.DownloadCount = downloadCount;
            InsertPackage(package);

            await indexer.AddPackage(package);

            Assert.Equal(downloadCount, datasource.First().DownloadCount);
        }

        [Fact]
        public void AddPackage_NewVersion_ZerosVersionDownloadCount()
        {
            var t1 = indexer.AddPackage(MakeSamplePackage("Sample.Package", "1.0"));
            var t2 = indexer.AddPackage(MakeSamplePackage("Sample.Package", "1.1"));

            Task.WaitAll(t1, t2);

            var packages = datasource.OrderBy(p => p.Version).ToArray();
            Assert.Equal(0, packages.Last().VersionDownloadCount);
        }

    }
}
