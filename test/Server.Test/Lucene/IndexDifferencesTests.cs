using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using NuGet;
using NuGet.Server.Infrastructure.Lucene;
using Xunit;

namespace Server.Test.Lucene
{
    public class IndexDifferencesTests
    {
        private readonly Mock<IFileSystem> fileSystem;
        private static readonly DateTimeOffset SamplePublishedDate = new DateTimeOffset(2012, 5, 29, 13, 42, 23, TimeSpan.Zero);

        public IndexDifferencesTests()
        {
            fileSystem = new Mock<IFileSystem>();
        }

        [Fact]
        public void Empty_NoMissingPackages()
        {
            SetupFileSystemPackagePaths();
            var indexedPackages = CreateLucenePackages();

            var diff = IndexDifferences.FindDifferences(fileSystem.Object, indexedPackages);

            Assert.Equal(0, diff.MissingPackages.Count());
        }

        [Fact]
        public void Empty_NoNewPackages()
        {
            SetupFileSystemPackagePaths();
            var indexedPackages = CreateLucenePackages();

            var diff = IndexDifferences.FindDifferences(fileSystem.Object, indexedPackages);

            Assert.Equal(0, diff.NewPackages.Count());
        }

        [Fact]
        public void Empty_NoModifiedPackages()
        {
            SetupFileSystemPackagePaths();
            var indexedPackages = CreateLucenePackages();

            var diff = IndexDifferences.FindDifferences(fileSystem.Object, indexedPackages);

            Assert.Equal(0, diff.ModifiedPackages.Count());
        }

        [Fact]
        public void NewPackages()
        {
            SetupFileSystemPackagePaths("a", "b");
            var indexedPackages = CreateLucenePackages("a");

            var diff = IndexDifferences.FindDifferences(fileSystem.Object, indexedPackages);

            Assert.Equal(new[] {"b"}, diff.NewPackages);
        }

        [Fact]
        public void MissingPackages()
        {
            SetupFileSystemPackagePaths("b", "c");
            var indexedPackages = CreateLucenePackages("a", "b");

            var diff = IndexDifferences.FindDifferences(fileSystem.Object, indexedPackages);

            Assert.Equal(new[] { "a" }, diff.MissingPackages);
        }

        [Fact]
        public void UpdatedPackages()
        {
            SetupFileSystemPackagePaths("a", "b");
            var indexedPackages = CreateLucenePackages("a", "b").ToList();
            indexedPackages[0].Published = SamplePublishedDate;
            var diff = IndexDifferences.FindDifferences(fileSystem.Object, indexedPackages);

            Assert.Equal(new[] { "b" }, diff.ModifiedPackages);
        }

        private IEnumerable<LucenePackage> CreateLucenePackages(params string[] paths)
        {
            foreach (var p in paths)
            {
                yield return new LucenePackage(fileSystem.Object) { Path = p };
            }
        }

        private void SetupFileSystemPackagePaths(params string[] paths)
        {
            fileSystem.Setup(fs => fs.GetLastModified(It.IsAny<string>())).Returns(SamplePublishedDate.ToLocalTime);
            fileSystem.Setup(fs => fs.GetFiles(string.Empty, "*" + Constants.PackageExtension, true)).Returns(paths).Verifiable();
        }
    }
}