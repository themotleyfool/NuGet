using System;
using Xunit;

namespace NuGet.Test.Server.Infrastructure
{

    public class HelpersTest
    {
        [Fact]
        public void GetRepositoryUrlCreatesProperUrlWithRootWebApp()
        {
            // Arrange
            Uri url = new Uri("http://example.com/default.aspx");
            string applicationPath = "/";

            // Act
            string repositoryUrl = Helpers.GetRepositoryUrl(url, applicationPath);

            // Assert
            Assert.Equal("http://example.com/", repositoryUrl);
        }

        [Fact]
        public void GetRepositoryUrlCreatesProperUrlWithVirtualApp()
        {
            // Arrange
            Uri url = new Uri("http://example.com/Foo/default.aspx");
            string applicationPath = "/Foo";

            // Act
            string repositoryUrl = Helpers.GetRepositoryUrl(url, applicationPath);

            // Assert
            Assert.Equal("http://example.com/Foo/", repositoryUrl);
        }

        [Fact]
        public void GetRepositoryUrlWithNonStandardPortCreatesProperUrlWithRootWebApp()
        {
            // Arrange
            Uri url = new Uri("http://example.com:1337/default.aspx");
            string applicationPath = "/";

            // Act
            string repositoryUrl = Helpers.GetRepositoryUrl(url, applicationPath);

            // Assert
            Assert.Equal("http://example.com:1337/", repositoryUrl);
        }

        [Fact]
        public void GetRepositoryUrlWithNonStandardPortCreatesProperUrlWithVirtualApp()
        {
            // Arrange
            Uri url = new Uri("http://example.com:1337/Foo/default.aspx");
            string applicationPath = "/Foo";

            // Act
            string repositoryUrl = Helpers.GetRepositoryUrl(url, applicationPath);

            // Assert
            Assert.Equal("http://example.com:1337/Foo/", repositoryUrl);
        }
    }
}
