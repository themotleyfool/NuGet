using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Linq;
using NuGet;
using NuGet.Server.Infrastructure.Lucene;
using Xunit;

namespace Server.Test.Lucene
{
    public class PackageIndexerIncrementDownloadCountTests : PackageIndexerTestBase
    {
        private const string SampleId = "Sample.Package";
        private const string OtherSampleId = "Other.Package";
        private const string SampleVersion1 = "1.1";
        private const string SampleVersion2 = "2.0";

        public PackageIndexerIncrementDownloadCountTests()
        {
            InsertPackage(SampleId, SampleVersion1);
            InsertPackage(SampleId, SampleVersion2);
        }

        [Fact]
        public void Apply_IncrementDownloadCountOnAllPackageVersions()
        {
            using (var session = OpenSession())
            {
                indexer.ApplyPendingDownloadIncrements(new List<LucenePackage> { MakeSamplePackage(SampleId, SampleVersion1) }, session);
            }

            Assert.True(datasource.ToList().All(p => p.DownloadCount == 1));
        }

        [Fact]
        public void Apply_LeavesOtherPackages()
        {
            InsertPackage(OtherSampleId, SampleVersion1);

            using (var session = OpenSession())
            {
                indexer.ApplyPendingDownloadIncrements(new List<LucenePackage> { MakeSamplePackage(SampleId, SampleVersion1) }, session);
            }

            Assert.Equal(0, GetPackage(OtherSampleId, SampleVersion1).DownloadCount);
            Assert.Equal(0, GetPackage(OtherSampleId, SampleVersion1).VersionDownloadCount);
        }

        [Fact]
        public void Apply_IncrementVersionDownloadCountOnlyOnSamePackageVersion()
        {
            using (var session = OpenSession())
            {
                indexer.ApplyPendingDownloadIncrements(new List<LucenePackage> { MakeSamplePackage(SampleId, SampleVersion2) }, session);
            }
            Assert.Equal(0, GetPackage(SampleId, SampleVersion1).VersionDownloadCount);
            Assert.Equal(1, GetPackage(SampleId, SampleVersion2).VersionDownloadCount);
        }

        [Fact]
        public void Apply_IncrementByThree()
        {
            var package = MakeSamplePackage(SampleId, SampleVersion2);
            using (var session = OpenSession())
            {
                indexer.ApplyPendingDownloadIncrements(new List<LucenePackage> {package, package, package}, session);
            }
            Assert.Equal(0, GetPackage(SampleId, SampleVersion1).VersionDownloadCount);
            Assert.Equal(3, GetPackage(SampleId, SampleVersion1).DownloadCount);
            Assert.Equal(3, GetPackage(SampleId, SampleVersion2).DownloadCount);
            Assert.Equal(3, GetPackage(SampleId, SampleVersion2).VersionDownloadCount);
        }

        private ISession<LucenePackage> OpenSession()
        {
            return provider.OpenSession(() => new LucenePackage(fileSystem.Object));
        }

        [Fact]
        public void IncrementQueuesForLater()
        {
            var task = indexer.IncrementDownloadCount(MakeSamplePackage(SampleId, SampleVersion1));
            Assert.Equal(task.IsCompleted, false);
        }

        [Fact]
        public void IncrementThrowsOnBlankId()
        {
            Assert.Throws<InvalidOperationException>(() => indexer.IncrementDownloadCount(MakeSamplePackage("", SampleVersion1)));
        }

        [Fact]
        public void IncrementThrowsOnNullId()
        {
            Assert.Throws<InvalidOperationException>(() => indexer.IncrementDownloadCount(MakeSamplePackage(null, SampleVersion1)));
        }

        [Fact]
        public void IncrementThrowsOnNullVersion()
        {
            Assert.Throws<InvalidOperationException>(() => indexer.IncrementDownloadCount(MakeSamplePackage(SampleId, null)));
        }

        private LucenePackage GetPackage(string id, string version)
        {
            return datasource.First(p => p.Id == id && p.Version == new SemanticVersion(version));
        }
    }
}