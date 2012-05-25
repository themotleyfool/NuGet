using System;
using System.IO;
using System.Linq;
using Lucene.Net.Linq;
using Lucene.Net.Store;
using Moq;
using NuGet;
using NuGet.Server.Infrastructure.Lucene;
using Xunit;
using LuceneDirectory = Lucene.Net.Store.Directory;

namespace Server.Test.Lucene
{
    public class LucenePackageRepositoryTest
    {
        private readonly LucenePackageRepository repository;
        private readonly Mock<IPackagePathResolver> packagePathResolver;
        private readonly Mock<IFileSystem> fileSystem;
        private readonly IQueryable<LucenePackage> datasource;
        private LuceneDataProvider provider;

        public LucenePackageRepositoryTest()
        {
            packagePathResolver = new Mock<IPackagePathResolver>();
            fileSystem = new Mock<IFileSystem>();

            repository = new LucenePackageRepository(packagePathResolver.Object, fileSystem.Object, new RAMDirectory());
            repository.HashProvider = new CryptoHashProvider();

            packagePathResolver.Setup(p => p.GetPackageDirectory(It.IsAny<IPackage>())).Returns("package-dir");
            Func<IPackage, string> thing = pkg => pkg.Id;
            packagePathResolver.Setup(p => p.GetPackageFileName(It.IsAny<IPackage>())).Returns(thing);

            provider = repository.Provider;
            datasource = repository.LucenePackages;
        }

        [Fact]
        public void ShouldCreateDirectory_NoSuchDirectoryException()
        {
            var dir = new Mock<LuceneDirectory>();
            dir.Setup(d => d.ListAll()).Throws(new NoSuchDirectoryException("no such!"));

            Assert.True(LucenePackageRepository.ShouldCreateIndex(dir.Object), "ShouldCreateIndex");
        }

        [Fact]
        public void ShouldCreateDirectory_Empty()
        {
            var dir = new Mock<LuceneDirectory>();
            dir.Setup(d => d.ListAll()).Returns(new string[0]);

            Assert.True(LucenePackageRepository.ShouldCreateIndex(dir.Object), "ShouldCreateIndex");
        }

        [Fact]
        public void ShouldCreateDirectory_NotEmpty()
        {
            var dir = new Mock<LuceneDirectory>();
            dir.Setup(d => d.ListAll()).Returns(new string[] {"I exist!"});

            Assert.False(LucenePackageRepository.ShouldCreateIndex(dir.Object), "ShouldCreateIndex");
        }

        [Fact]
        public void AddPackage()
        {
            repository.AddPackage(MakeSamplePackage("Sample.Package", "1.0"), new byte[0]);

            Assert.Equal(1, datasource.Count());
        }

        [Fact]
        public void AddPackage_SetPublishedDate()
        {
            var now = DateTimeOffset.UtcNow;

            repository.AddPackage(MakeSamplePackage("Sample.Package", "1.0"), new byte[0]);

            Assert.True(datasource.First().Published.HasValue, "Published.HasValue");
            Assert.InRange(datasource.First().Published.Value, now, now.AddMinutes(1));
        }

        [Fact]
        public void AddPackage_SetIsLatestVersion()
        {
            repository.AddPackage(MakeSamplePackage("Sample.Package", "1.0"), new byte[0]);

            Assert.True(datasource.First().IsLatestVersion, "IsLatestVersion");
        }

        [Fact]
        public void AddPackage_MultipleVersions_UnsetIsLatestVersion()
        {
            repository.AddPackage(MakeSamplePackage("Sample.Package", "1.0"), new byte[0]);
            repository.AddPackage(MakeSamplePackage("Sample.Package", "1.1"), new byte[0]);

            var packages = datasource.OrderBy(p => p.Version).ToArray();
            Assert.False(packages.First().IsLatestVersion, "older.IsLatestVersion");
            Assert.True(packages.Last().IsLatestVersion, "newer.IsLatestVersion");
        }

        [Fact]
        public void AddPackage_Replaces()
        {
            InsertPackage("Sample.Package", "1.0");

            repository.AddPackage(MakeSamplePackage("Sample.Package", "1.0"), new byte[0]);

            Assert.Equal(1, datasource.Count());
        }

        [Fact]
        public void AddPackage_ReplaceZerosVersionDownloadCount()
        {
            const int versionDownloadCount = 199;
            var package = MakeSamplePackage("Sample.Package", "1.0");
            package.VersionDownloadCount = versionDownloadCount;
            InsertPackage(package);

            repository.AddPackage(package, new byte[0]);

            Assert.Equal(0, datasource.First().VersionDownloadCount);
        }

        [Fact]
        public void AddPackage_ReplacePreservesDownloadCount()
        {
            const int downloadCount = 23999;
            var package = MakeSamplePackage("Sample.Package", "1.0");
            package.DownloadCount = downloadCount;
            InsertPackage(package);

            repository.AddPackage(package, new byte[0]);

            Assert.Equal(downloadCount, datasource.First().DownloadCount);
        }

        private LucenePackage MakeSamplePackage(string id, string version)
        {
            return new LucenePackage(fileSystem.Object, path => new MemoryStream())
                       {
                           Id = id,
                           Version = new SemanticVersion(version)
                       };
        }

        private void InsertPackage(string id, string version)
        {
            var p = MakeSamplePackage(id, version);

            InsertPackage(p);
        }

        private void InsertPackage(LucenePackage p)
        {
            p.Path = Path.Combine(packagePathResolver.Object.GetPackageDirectory(p),
                                  packagePathResolver.Object.GetPackageFileName(p));

            using (var s = provider.OpenSession<LucenePackage>())
            {
                s.Add(p);
                s.Commit();
            }
        }
    }
}
