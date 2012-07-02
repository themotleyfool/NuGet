using System;
using Lucene.Net.Index;
using Lucene.Net.Linq;
using Lucene.Net.Search;
using Moq;
using NuGet.Server.Infrastructure.Lucene;
using Xunit;

namespace Server.Test.Lucene
{
    public class PackageIndexerSynchronizeTests : PackageIndexerTestBase
    {
        private static readonly string[] Empty = new string[0];
        private readonly Mock<ISession<LucenePackage>> session;

        public PackageIndexerSynchronizeTests()
        {
            session = new Mock<ISession<LucenePackage>>();
            session.Setup(s => s.Query()).Returns(datasource);
        }

        [Fact]
        public void DoesNothingOnNoDifferences()
        {
            indexer.SynchronizeIndexWithFileSystem(new IndexDifferences(Empty, Empty, Empty), session.Object);

            session.Verify();
        }

        [Fact]
        public void DeletesMissingPackages()
        {
            var missing = new[] {"A.nupkg", "B.nupkg"};

            session.Setup(s => s.Delete(new TermQuery(new Term("Path", "A.nupkg")), new TermQuery(new Term("Path", "B.nupkg")))).Verifiable();

            indexer.SynchronizeIndexWithFileSystem(new IndexDifferences(Empty, missing, Empty), session.Object);

            session.Verify();
        }

        [Fact]
        public void AddsNewPackages()
        {
            var newPackages = new[] { "A.1.0.nupkg" };

            var pkg = MakeSamplePackage("A", "1.0");
            loader.Setup(l => l.LoadFromFileSystem(newPackages[0])).Returns(pkg);

            session.Setup(s => s.Add(It.IsAny<LucenePackage>())).Verifiable();

            session.Setup(s => s.Commit()).Verifiable();

            indexer.SynchronizeIndexWithFileSystem(new IndexDifferences(newPackages, Empty, Empty), session.Object);

            session.VerifyAll();
        }

        [Fact]
        public void ContinuesOnException()
        {
            var newPackages = new[] { "A.1.0.nupkg", "B.1.0.nupkg" };

            var pkg = MakeSamplePackage("B", "1.0");

            loader.Setup(l => l.LoadFromFileSystem(newPackages[0])).Throws(new Exception("invalid package"));
            loader.Setup(l => l.LoadFromFileSystem(newPackages[1])).Returns(pkg);

            session.Setup(s => s.Add(It.IsAny<LucenePackage>())).Verifiable();

            session.Setup(s => s.Commit()).Verifiable();

            indexer.SynchronizeIndexWithFileSystem(new IndexDifferences(newPackages, Empty, Empty), session.Object);

            session.VerifyAll();
        }
    }
}