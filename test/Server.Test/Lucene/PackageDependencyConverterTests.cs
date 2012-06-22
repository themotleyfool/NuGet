using NuGet;
using NuGet.Server.Infrastructure.Lucene.Mapping;
using Xunit;

namespace Server.Test.Lucene
{
    public class PackageDependencyConverterTests
    {
        private readonly PackageDependencyConverter converter = new PackageDependencyConverter();

        [Fact]
        public void Convert()
        {
            var result = (PackageDependency)converter.ConvertFrom(null, null, "MyPackage:1.0.0.0");

            Assert.Equal("MyPackage", result.Id);
            Assert.Equal("1.0.0.0", result.VersionSpec.ToString());
        }

        [Fact]
        public void Convert_BlankVersion()
        {
            var result = (PackageDependency)converter.ConvertFrom(null, null, "MyPackage:");

            Assert.Equal("MyPackage", result.Id);

            Assert.Equal(VersionUtility.ParseVersionSpec(PackageDependencyConverter.AnyVersionSpec).ToString(), result.VersionSpec.ToString());
        }
    }
}