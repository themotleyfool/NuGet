using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Lucene.Net.Linq.Mapping;
using NuGet.Server.Infrastructure.Lucene.Mapping;

namespace NuGet.Server.Infrastructure.Lucene
{
    public class LucenePackage : IPackage
    {
        private readonly IFileSystem fileSystem;
        private readonly Func<string, Stream> getStream;

        public LucenePackage(IFileSystem fileSystem)
            : this(fileSystem, fileSystem.OpenFile)
        {
        }

        public LucenePackage(IFileSystem fileSystem, Func<string, Stream> getStream)
        {
            this.fileSystem = fileSystem;
            this.getStream = getStream;

            Listed = true;

            Authors = Enumerable.Empty<string>();
            Owners = Enumerable.Empty<string>();
            AssemblyReferences = Enumerable.Empty<IPackageAssemblyReference>();
            FrameworkAssemblies = Enumerable.Empty<FrameworkAssemblyReference>();
            Dependencies = Enumerable.Empty<string>();
        }

        [QueryScore]
        public float Score { get; set; }

        #region IPackage

        [Field(Key = true)]
        public string Id { get; set; }

        [Field(Key = true)]
        public SemanticVersion Version { get; set; }

        public string Title { get; set; }

        [Field(IndexMode.NotIndexed)]
        public Uri IconUrl { get; set; }

        [Field(IndexMode.NotIndexed)]
        public Uri LicenseUrl { get; set; }

        [Field(IndexMode.NotIndexed)]
        public Uri ProjectUrl { get; set; }

        [Field(IndexMode.NotIndexed)]
        public Uri ReportAbuseUrl { get; set; }

        [NumericField(Converter = typeof(BoolToIntConverter))]
        public bool RequireLicenseAcceptance { get; set; }

        public string Description { get; set; }

        public string Summary { get; set; }

        public string ReleaseNotes { get; set; }

        [Field(IndexMode.NotIndexed)]
        public string Language { get; set; }

        public string Tags { get; set; }

        [Field(IndexMode.NotIndexed)]
        public string Copyright { get; set; }

        [NumericField]
        public int DownloadCount { get; set; }

        [NumericField]
        public int VersionDownloadCount { get; set; }

        [NumericField(Converter = typeof(BoolToIntConverter))]
        public bool IsAbsoluteLatestVersion { get; set; }

        [NumericField(Converter = typeof(BoolToIntConverter))]
        public bool IsLatestVersion { get; set; }

        [IgnoreField]
        public bool Listed { get; set; }

        [NumericField(Converter = typeof(BoolToIntConverter))]
        public bool IsPrerelease
        {
            get { return !this.IsReleaseVersion(); }
        }

        [NumericField(Converter = typeof(DateTimeOffsetToTicksConverter))]
        public DateTimeOffset? Published { get; set; }

        public IEnumerable<string> Authors { get; set; }

        public IEnumerable<string> Owners { get; set; }

        public IEnumerable<string> Dependencies { get; set; }

        [IgnoreField]
        public IEnumerable<PackageDependencySet> DependencySets
        {
            get { return PackageDependencySetConverter.Parse(Dependencies); }
            set { Dependencies = value.SelectMany(PackageDependencySetConverter.Flatten); }
        }

        [IgnoreField]
        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies { get; set; }
        
        [IgnoreField]
        public IEnumerable<IPackageAssemblyReference> AssemblyReferences { get; set; }

        public IEnumerable<IPackageFile> GetFiles()
        {
            throw new NotImplementedException();
        }

        public Stream GetStream()
        {
            return getStream(Path);
        }

        #endregion

        #region DerivedPackageData

        [NumericField]
        public long PackageSize { get; set; }

        [Field(IndexMode.NotIndexed)]
        public string PackageHash { get; set; }

        [NumericField(Converter = typeof(DateTimeOffsetToTicksConverter))]
        public DateTimeOffset LastUpdated { get; set; }

        [NumericField(Converter = typeof(DateTimeOffsetToTicksConverter))]
        public DateTimeOffset Created { get; set; }

        [Field(IndexMode.NotAnalyzed)]
        public string Path { get; set; }

        [Field(Converter = typeof(FrameworkNameConverter))]
        public IEnumerable<FrameworkName> SupportedFrameworks { get; set; }

        [IgnoreField]
        public string FullPath
        {
            get { return (Path != null) ? fileSystem.GetFullPath(Path) : null; }
        }

        #endregion
    }
}