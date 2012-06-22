using System.Linq;
using System.Runtime.Versioning;
using NuGet;
using NuGet.Server.Infrastructure.Lucene.Mapping;
using Xunit;

namespace Server.Test.Lucene.Mapping
{
    public class PackageDependencySetConverterTests
    {
        private readonly PackageDependency NoConstraint =
            new PackageDependency("id1");
        private readonly PackageDependency ExactVersion =
            new PackageDependency("id2", new VersionSpec(new SemanticVersion("1.0")));

        [Fact]
        public void Flatten()
        {
            var frameworkName = VersionUtility.ParseFrameworkName("net35");
            var actual = PackageDependencySetConverter.Flatten(new PackageDependencySet(frameworkName, new[] { NoConstraint, ExactVersion}));

            Assert.Equal(new[] { "id1::net35", "id2:[1.0]:net35" }, actual);
        }

        [Fact]
        public void Flatten_NoDependenciesForFramework()
        {
            var frameworkName = VersionUtility.ParseFrameworkName("net35");
            var actual = PackageDependencySetConverter.Flatten(new PackageDependencySet(frameworkName, new PackageDependency[0]));

            Assert.Equal(new[] { "::net35" }, actual);
        }

        [Fact]
        public void Flatten_TargetFrameworkNull()
        {
            var actual = PackageDependencySetConverter.Flatten(new PackageDependencySet(null, new[] { NoConstraint, ExactVersion, }));

            Assert.Equal(new[] { "id1::", "id2:[1.0]:" }, actual);
        }

        [Fact]
        public void ToDependencySets()
        {
            var results = PackageDependencySetConverter.Parse(new[] { "id1", "id3::net35", "id2:1.0", "id4:[1.1,2.0):net20" }).ToList();
            
            Assert.Equal(3, results.Count());

            results.Sort((a, b) => a.TargetFramework.FullNameOrBlank().CompareTo(b.TargetFramework.FullNameOrBlank()));

            Assert.Null(results[0].TargetFramework);
            Assert.Equal(new[] { "id1", "id2" }, results[0].Dependencies.Select(d => d.Id));
            Assert.Equal(new[] { null, VersionUtility.ParseVersionSpec("1.0") }, results[0].Dependencies.Select(d => d.VersionSpec));

            Assert.Equal("net20", VersionUtility.GetShortFrameworkName(results[1].TargetFramework));
            Assert.Equal(new[] { "id4" }, results[1].Dependencies.Select(d => d.Id));
            Assert.Equal(new[] { VersionUtility.ParseVersionSpec("[1.1,2.0)") }, results[1].Dependencies.Select(d => d.VersionSpec));

            Assert.Equal("net35", VersionUtility.GetShortFrameworkName(results[2].TargetFramework));
            Assert.Equal(new[] { "id3" }, results[2].Dependencies.Select(d => d.Id));
            Assert.Equal(new IVersionSpec[] { null }, results[2].Dependencies.Select(d => d.VersionSpec));
        }

        [Fact]
        public void ToDependencySets_EmptySet()
        {
            var results = PackageDependencySetConverter.Parse(new[] { "::net40" }).ToList();

            Assert.Equal(1, results.Count());
            Assert.Equal("net40", VersionUtility.GetShortFrameworkName(results[0].TargetFramework));
            Assert.Equal(0, results[0].Dependencies.Count());
        }
    }

    static class Helper
    {
        public static string FullNameOrBlank(this FrameworkName frameworkName)
        {
            return frameworkName == null ? "" : frameworkName.FullName;
        }
    }
}