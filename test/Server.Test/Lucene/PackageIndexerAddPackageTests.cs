using System;
using System.Linq;
using Moq;
using Xunit;

namespace Server.Test.Lucene
{
    public class PackageIndexerAddPackageTests : PackageIndexerTestBase
    {
        [Fact]
        public void AddPackage()
        {
            indexer.AddPackage(MakeSamplePackage("Sample.Package", "1.0"));

            Assert.Equal(1, datasource.Count());
        }

        [Fact]
        public void AddPackage_SetIsLatestVersion()
        {
            indexer.AddPackage(MakeSamplePackage("Sample.Package", "1.0"));

            Assert.True(datasource.First().IsLatestVersion, "IsLatestVersion");
        }

        [Fact]
        public void AddPackage_MultipleVersions_UnsetIsLatestVersion()
        {
            indexer.AddPackage(MakeSamplePackage("Sample.Package", "1.0"));
            indexer.AddPackage(MakeSamplePackage("Sample.Package", "1.1"));

            var packages = datasource.OrderBy(p => p.Version).ToArray();
            Assert.False(packages.First().IsLatestVersion, "older.IsLatestVersion");
            Assert.True(packages.Last().IsLatestVersion, "newer.IsLatestVersion");
        }

        [Fact]
        public void AddPackage_Replaces()
        {
            InsertPackage("Sample.Package", "1.0");

            indexer.AddPackage(MakeSamplePackage("Sample.Package", "1.0"));

            Assert.Equal(1, datasource.Count());
        }

        [Fact]
        public void AddPackage_ReplaceZerosVersionDownloadCount()
        {
            const int versionDownloadCount = 199;
            var package = MakeSamplePackage("Sample.Package", "1.0");
            package.VersionDownloadCount = versionDownloadCount;
            InsertPackage(package);

            indexer.AddPackage(package);

            Assert.Equal(0, datasource.First().VersionDownloadCount);
        }

        [Fact]
        public void AddPackage_ReplacePreservesDownloadCount()
        {
            const int downloadCount = 23999;
            var package = MakeSamplePackage("Sample.Package", "1.0");
            package.DownloadCount = downloadCount;
            InsertPackage(package);

            indexer.AddPackage(package);

            Assert.Equal(downloadCount, datasource.First().DownloadCount);
        }
    }
}
