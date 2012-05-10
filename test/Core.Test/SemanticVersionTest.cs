﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Xunit;
using Xunit.Extensions;

namespace NuGet.Test
{
    public class SemanticVersionTest
    {
        [Theory]
        [PropertyData("ConstructorData")]
        public void StringConstructorParsesValuesCorrectly(string version, Version versionValue, string specialValue)
        {
            // Act
            SemanticVersion semanticVersion = new SemanticVersion(version);

            // Assert
            Assert.Equal(versionValue, semanticVersion.Version);
            Assert.Equal(specialValue, semanticVersion.SpecialVersion);
            Assert.Equal(version, semanticVersion.ToString());
        }

        public static IEnumerable<object[]> ConstructorData
        {
            get
            {
                yield return new object[] { "1.0.0", new Version("1.0.0.0"), "" };
                yield return new object[] { "2.3-alpha", new Version("2.3.0.0"), "alpha" };
                yield return new object[] { "3.4.0.3-RC-3", new Version("3.4.0.3"), "RC-3" };
            }
        }

        [Fact]
        public void ParseThrowsIfStringIsNullOrEmpty()
        {
            ExceptionAssert.ThrowsArgNullOrEmpty(() => SemanticVersion.Parse(version: null), "version");
            ExceptionAssert.ThrowsArgNullOrEmpty(() => SemanticVersion.Parse(version: String.Empty), "version");
        }

        [Theory]
        [InlineData("1")]
        [InlineData("1beta")]
        [InlineData("1.2Av^c")]
        [InlineData("1.2..")]
        [InlineData("1.2.3.4.5")]
        [InlineData("1.2.3.Beta")]
        [InlineData("1.2.3.4This version is full of awesomeness!!")]
        [InlineData("So.is.this")]
        [InlineData("1.34.2Alpha")]
        [InlineData("1.34.2Release Candidate")]
        [InlineData("1.4.7-")]
        public void ParseThrowsIfStringIsNotAValidSemVer(string versionString)
        {
            ExceptionAssert.ThrowsArgumentException(() => SemanticVersion.Parse(versionString),
                "version",
                String.Format(CultureInfo.InvariantCulture, "'{0}' is not a valid version string.", versionString));
        }

        public static IEnumerable<object[]> LegacyVersionData
        {
            get
            {
                yield return new object[] { "1.022", new SemanticVersion(new Version("1.22.0.0"), "") };
                yield return new object[] { "23.2.3", new SemanticVersion(new Version("23.2.3.0"), "") };
                yield return new object[] { "1.3.42.10133", new SemanticVersion(new Version("1.3.42.10133"), "") };
            }
        }

        [Theory]
        [PropertyData("LegacyVersionData")]
        public void ParseReadsLegacyStyleVersionNumbers(string versionString, SemanticVersion expected)
        {
            // Act
            var actual = SemanticVersion.Parse(versionString);

            // Assert
            Assert.Equal(expected.Version, actual.Version);
            Assert.Equal(expected.SpecialVersion, actual.SpecialVersion);
        }

        public static IEnumerable<object[]> SemVerData
        {
            get
            {
                yield return new object[] { "1.022-Beta", new SemanticVersion(new Version("1.22.0.0"), "Beta") };
                yield return new object[] { "23.2.3-Alpha", new SemanticVersion(new Version("23.2.3.0"), "Alpha") };
                yield return new object[] { "1.3.42.10133-PreRelease", new SemanticVersion(new Version("1.3.42.10133"), "PreRelease") };
                yield return new object[] { "1.3.42.200930-RC-2", new SemanticVersion(new Version("1.3.42.200930"), "RC-2") };
            }
        }

        [Theory]
        [PropertyData("SemVerData")]
        public void ParseReadsSemverAndHybridSemverVersionNumbers(string versionString, SemanticVersion expected)
        {
            // Act
            var actual = SemanticVersion.Parse(versionString);

            // Assert
            Assert.Equal(expected.Version, actual.Version);
            Assert.Equal(expected.SpecialVersion, actual.SpecialVersion);
        }

        [Theory]
        [PropertyData("SemVerData")]
        public void TypeConverterIsRegistered(string versionString, SemanticVersion expected)
        {
            // Act
            var converter = TypeDescriptor.GetConverter(typeof (SemanticVersion));

            // Assert
            Assert.NotNull(converter);
            Assert.True(converter.CanConvertFrom(typeof(string)));
        }

        [Theory]
        [PropertyData("SemVerData")]
        public void ConvertToString(string expected, SemanticVersion version)
        {
            // Act
            var converter = TypeDescriptor.GetConverter(typeof (SemanticVersion));
            var actual = converter.ConvertTo(version, typeof(string));

            // Assert
            Assert.Equal(version.ToString(), actual);
        }

        [Theory]
        [PropertyData("SemVerData")]
        public void ConvertFromString(string versionString, SemanticVersion expected)
        {
            // Act
            var converter = TypeDescriptor.GetConverter(typeof(SemanticVersion));
            var actual = converter.ConvertFrom(versionString);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> SemVerWithWhiteSpace
        {
            get
            {
                yield return new object[] { "  1.022-Beta", new SemanticVersion(new Version("1.22.0.0"), "Beta") };
                yield return new object[] { "23.2.3-Alpha  ", new SemanticVersion(new Version("23.2.3.0"), "Alpha") };
                yield return new object[] { "    1.3.42.10133-PreRelease  ", new SemanticVersion(new Version("1.3.42.10133"), "PreRelease") };
            }
        }

        [Theory]
        [PropertyData("SemVerWithWhiteSpace")]
        public void ParseIgnoresLeadingAndTrailingWhitespace(string versionString, SemanticVersion expected)
        {
            // Act
            var actual = SemanticVersion.Parse(versionString);

            // Assert
            Assert.Equal(expected.Version, actual.Version);
            Assert.Equal(expected.SpecialVersion, actual.SpecialVersion);
        }

        [Theory]
        [InlineData("1.0", "1.0.1")]
        [InlineData("1.23", "1.231")]
        [InlineData("1.4.5.6", "1.45.6")]
        [InlineData("1.4.5.6", "1.4.5.60")]
        [InlineData("1.01", "1.10")]
        [InlineData("1.01-alpha", "1.10-beta")]
        [InlineData("1.01.0-RC-1", "1.10.0-rc-2")]
        [InlineData("1.01-RC-1", "1.01")]
        [InlineData("1.01", "1.2-preview")]
        public void SemVerLessThanAndGreaterThanOperatorsWorks(string versionA, string versionB)
        {
            // Arrange
            var itemA = new SemanticVersion(versionA);
            var itemB = new SemanticVersion(versionB);
            object objectB = itemB;

            // Act and Assert
            Assert.True(itemA < itemB);
            Assert.True(itemA <= itemB);
            Assert.True(itemB > itemA);
            Assert.True(itemB >= itemA);
            Assert.False(itemA.Equals(itemB));
            Assert.False(itemA.Equals(objectB));
        }

        [Theory]
        [InlineData(new object[] { 1 })]
        [InlineData(new object[] { "1.0.0" })]
        [InlineData(new object[] { new object[0] })]
        public void EqualsReturnsFalseIfComparingANonSemVerType(object other)
        {
            // Arrange
            var semVer = new SemanticVersion("1.0.0");

            // Act and Assert
            Assert.False(semVer.Equals(other));
        }

        [Fact]
        public void SemVerThrowsIfLeftHandExpressionForCompareOperatorsIsNull()
        {
            // Arrange
            SemanticVersion itemA = null;
            SemanticVersion itemB = new SemanticVersion("1.0");

            // Act and Assert
            ExceptionAssert.ThrowsArgNull(() => { bool val = itemA < itemB; }, "version1");
            ExceptionAssert.ThrowsArgNull(() => { bool val = itemA <= itemB; }, "version1");
            ExceptionAssert.ThrowsArgNull(() => { bool val = itemA > itemB; }, "version1");
            ExceptionAssert.ThrowsArgNull(() => { bool val = itemA >= itemB; }, "version1");
        }

        [Theory]
        [InlineData("1.0", "1.0.0.0")]
        [InlineData("1.23.01", "1.23.1")]
        [InlineData("1.45.6", "1.45.6.0")]
        [InlineData("1.45.6-Alpha", "1.45.6-Alpha")]
        [InlineData("1.6.2-BeTa", "1.6.02-beta")]
        [InlineData("22.3.07     ", "22.3.07")]
        public void SemVerEqualsOperatorWorks(string versionA, string versionB)
        {
            // Arrange
            var itemA = new SemanticVersion(versionA);
            var itemB = new SemanticVersion(versionB);
            object objectB = itemB;

            // Act and Assert
            Assert.True(itemA == itemB);
            Assert.True(itemA.Equals(itemB));
            Assert.True(itemA.Equals(objectB));
            Assert.True(itemA <= itemB);
            Assert.True(itemB == itemA);
            Assert.True(itemB >= itemA);
        }

        [Fact]
        public void SemVerEqualityComparisonsWorkForNullValues()
        {
            // Arrange
            SemanticVersion itemA = null;
            SemanticVersion itemB = null;

            // Act and Assert
            Assert.True(itemA == itemB);
            Assert.True(itemB == itemA);
            Assert.True(itemA <= itemB);
            Assert.True(itemB <= itemA);
            Assert.True(itemA >= itemB);
            Assert.True(itemB >= itemA);
        }

        [Theory]
        [InlineData("1.0")]
        [InlineData("1.0.0")]
        [InlineData("1.0.0.0")]
        [InlineData("1.0-alpha")]
        [InlineData("1.0.0-b")]
        [InlineData("3.0.1.2")]
        [InlineData("2.1.4.3-pre-1")]
        public void ToStringReturnsOriginalValue(string version)
        {
            // Act
            SemanticVersion semVer = new SemanticVersion(version);

            // Assert
            Assert.Equal(version, semVer.ToString());
        }

        public static IEnumerable<object[]> ToStringFromVersionData
        {
            get
            {
                yield return new object[] { new Version("1.0"), null, "1.0" };
                yield return new object[] { new Version("1.0.3.120"), String.Empty, "1.0.3.120" };
                yield return new object[] { new Version("1.0.3.120"), "alpha", "1.0.3.120-alpha" };
                yield return new object[] { new Version("1.0.3.120"), "rc-2", "1.0.3.120-rc-2" };
            }
        }

        [Theory]
        [PropertyDataAttribute("ToStringFromVersionData")]
        public void ToStringConstructedFromVersionAndSpecialVersionConstructor(Version version, string specialVersion, string expected)
        {
            // Act
            SemanticVersion semVer = new SemanticVersion(version, specialVersion);

            // Assert
            Assert.Equal(expected, semVer.ToString());
        }

        [Fact]
        public void TryParseStrictParsesStrictVersion()
        {
            // Arrange
            var versionString = "1.3.2-CTP-2-Refresh-Alpha";

            // Act
            SemanticVersion version;
            bool result = SemanticVersion.TryParseStrict(versionString, out version);

            // Assert
            Assert.True(result);
            Assert.Equal(new Version("1.3.2.0"), version.Version);
            Assert.Equal("CTP-2-Refresh-Alpha", version.SpecialVersion);
        }

        [Theory]
        [InlineData("2.7")]
        [InlineData("1.3.4.5")]
        [InlineData("1.3-alpha")]
        [InlineData("1.3 .4")]
        [InlineData("2.3.18.2-a")]
        public void TryParseStrictReturnsFalseIfVersionIsNotStrictSemVer(string version)
        {
            // Act 
            SemanticVersion semanticVersion;
            bool result = SemanticVersion.TryParseStrict(version, out semanticVersion);

            // Assert
            Assert.False(result);
            Assert.Null(semanticVersion);
        }
    }
}
