using Lucene.Net.Store;
using Moq;
using NuGet.Server.Infrastructure;
using Xunit;

namespace Server.Test
{
    public class BindingTests
    {
        [Fact]
        public void ShouldCreateDirectory_NoSuchDirectoryException()
        {
            var dir = new Mock<Directory>();
            dir.Setup(d => d.ListAll()).Throws(new NoSuchDirectoryException("no such!"));

            Assert.True(Bindings.ShouldCreateIndex(dir.Object), "ShouldCreateIndex");
        }

        [Fact]
        public void ShouldCreateDirectory_Empty()
        {
            var dir = new Mock<Directory>();
            dir.Setup(d => d.ListAll()).Returns(new string[0]);

            Assert.True(Bindings.ShouldCreateIndex(dir.Object), "ShouldCreateIndex");
        }

        [Fact]
        public void ShouldCreateDirectory_NotEmpty()
        {
            var dir = new Mock<Directory>();
            dir.Setup(d => d.ListAll()).Returns(new string[] { "I exist!" });

            Assert.False(Bindings.ShouldCreateIndex(dir.Object), "ShouldCreateIndex");
        }
    }
}