using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet;
using NuGet.Server.Controllers;
using NuGet.Server.Infrastructure.Lucene;
using NuGet.Test.Mocks;
using Xunit;
using Xunit.Sdk;

namespace Server.Test.Controllers
{
    public class TabCompletionControllerTests
    {
        private readonly TabCompletionController controller;
        private readonly Mock<ILucenePackageRepository> repository;
        private readonly List<LucenePackage> packages = new List<LucenePackage>();

        public TabCompletionControllerTests()
        {
            repository = new Mock<ILucenePackageRepository>();
            controller = new TabCompletionController { Repository = repository.Object };

            repository.Setup(repo => repo.LucenePackages).Returns(packages.AsQueryable());
        }

        [Fact]
        public void GetMatchingPackages_NoPackages()
        {
            var result = controller.GetMatchingPackages(null, false, 30);

            AssertSequenceEqual(Enumerable.Empty<string>(), result.Data);
        }

        [Fact]
        public void GetMatchingPackages_LimitsTo30Distinct()
        {
            var packageIds = Enumerable.Range(0, 100).Select(i => "Foo-" + i.ToString("D3")).ToList();
            
            Enumerable.Range(1, 5).ToList().ForEach(version => packageIds.ForEach(i => AddSamplePackage(i, version + ".0")));

            var result = controller.GetMatchingPackages("F", false, 30);

            AssertSequenceEqual(packageIds.Take(30), result.Data);
        }

        [Fact]
        public void GetMatchingPackages_ExcludePrerelease()
        {
            AddSamplePackage("Foo", "1.0-alpha");
            AddSamplePackage("Bar", "1.0");

            var result = controller.GetMatchingPackages("", false, 30);

            AssertSequenceEqual(new[] {"Bar"}, result.Data);
        }

        [Fact]
        public void GetMatchingPackages_IncludePrerelease()
        {
            AddSamplePackage("Foo", "1.0-alpha");

            var result = controller.GetMatchingPackages("F", true, 30);

            AssertSequenceEqual(new[] { "Foo" }, result.Data);
        }

        [Fact]
        public void GetMatchingPackages_OrderById()
        {
            AddSamplePackage("Zoo", "1.0");
            AddSamplePackage("Foo", "1.0");

            var result = controller.GetMatchingPackages("", false, 30);

            AssertSequenceEqual(new[] { "Foo", "Zoo" }, result.Data);
        }

        private void AssertSequenceEqual(IEnumerable expected, object actual)
        {
            if (!(actual is IEnumerable)) throw new AssertException("Expected type implementing IEnumerable but was " + actual.GetType());

            Assert.Equal(expected.Cast<object>().ToArray(), ((IEnumerable)actual).Cast<object>().ToArray());
        }

        [Fact]
        public void GetPackageVersions_NoMatch()
        {
            var result = controller.GetPackageVersions("Foo", false);

            Assert.Equal(Enumerable.Empty<string>(), result.Data);
        }

        [Fact]
        public void GetPackageVersions_OrdersByVersion()
        {
            AddSamplePackage("Foo", "2.0");
            AddSamplePackage("Foo", "1.0");

            var result = controller.GetPackageVersions("Foo", false);

            Assert.Equal(new[] {"1.0", "2.0"}, result.Data);
        }

        [Fact]
        public void GetPackageVersions_FiltersById()
        {
            AddSamplePackage("Foo", "2.0");
            AddSamplePackage("Bar", "1.0");

            var result = controller.GetPackageVersions("Foo", false);

            Assert.Equal(new[] { "2.0" }, result.Data);
        }

        [Fact]
        public void GetPackageVersions_FiltersByPrerelease()
        {
            AddSamplePackage("Foo", "2.0");
            AddSamplePackage("Foo", "3.0-alpha");

            var result = controller.GetPackageVersions("Foo", false);

            Assert.Equal(new[] { "2.0" }, result.Data);
        }

        [Fact]
        public void GetPackageVersions_IncludesPrerelease()
        {
            AddSamplePackage("Foo", "1.0");
            AddSamplePackage("Foo", "2.0-pre");

            var result = controller.GetPackageVersions("Foo", true);

            Assert.Equal(new[] { "1.0", "2.0-pre" }, result.Data);
        }

        private void AddSamplePackage(string id, string version)
        {
            var semanticVersion = new SemanticVersion(version);
            
            packages.Add(new LucenePackage(new MockFileSystem()) { Id = id, Version = semanticVersion });
        }
    }
}