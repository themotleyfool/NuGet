using System.Collections.Generic;
using NuGet.Server.Infrastructure.Lucene;
using Xunit;
using Xunit.Extensions;

namespace Server.Test.Lucene
{
    public class StrictSemanticVersionTests : TestBase
    {
        [Theory]
        [InlineData("1.0-alpha", "1.0-alpha")]
        [InlineData("1.0-BETA", "1.0-beta")]
        public void EquatabilityBasedOnOriginalString(string versionA, string versionB)
        {
            Assert.True(new StrictSemanticVersion(versionA).Equals(new StrictSemanticVersion(versionB)));
        }

        [Theory]
        [InlineData("1.0-alpha", "1.0-alpha")]
        [InlineData("1.0-BETA", "1.0-beta")]
        public void ObjectEqualsBasedOnOriginalString(string versionA, string versionB)
        {
            Assert.True(new StrictSemanticVersion(versionA).Equals((object) new StrictSemanticVersion(versionB)));
        }

        [Theory]
        [InlineData("1.0-alpha", "1.0-ALPHA")]
        public void HashCodesEqualWhenCaseNotSame(string versionA, string versionB)
        {
            Assert.Equal(new StrictSemanticVersion(versionA).GetHashCode(), new StrictSemanticVersion(versionB).GetHashCode());
        }

        [Theory]
        [PropertyData("StupidlyFormattedVersions")]
        public void StupidlyFormattedVersionsNotEqual(string versionA, string versionB)
        {
            Assert.False(new StrictSemanticVersion(versionA).Equals((object)new StrictSemanticVersion(versionB)), "Equals(object obj)");
        }

        [Theory]
        [PropertyData("StupidlyFormattedVersions")]
        public void StupidlyFormattedVersionsNotEqual_Equatable(string versionA, string versionB)
        {
            Assert.NotEqual(new StrictSemanticVersion(versionA), new StrictSemanticVersion(versionB));
        }

        public static IEnumerable<object[]> StupidlyFormattedVersions
        {
            get
            {
                yield return new object[] { "1.0", "1.0.0" };
                yield return new object[] { "2.00", "2.0" };
                yield return new object[] { "3.01", "3.1" };
                yield return new object[] { "4.0", "04.0" };
            }
        }
    }
}