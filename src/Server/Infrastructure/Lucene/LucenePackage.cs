using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Lucene.Net.Linq.Mapping;

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
            Dependencies = Enumerable.Empty<PackageDependency>();
        }

        /// <summary>
        /// Combination of all searchable text fields for indexing.
        /// </summary>
        [Field(Store = false)]
        public string Text
        {
            get
            {
                var fields = new List<string> { Id, Title, Description, Summary, ReleaseNotes, Tags };
                fields.AddRange(Authors);
                fields.AddRange(Owners);

                return string.Join(" ", fields.Where(f => !string.IsNullOrEmpty(f)));
            }
        }

        #region IPackage

        public string Id { get; set; }

        public SemanticVersion Version { get; set; }

        [Field(IndexMode.NotIndexed)]
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

        [Field(IndexMode.NotIndexed)]
        public string Description { get; set; }

        [Field(IndexMode.NotIndexed)]
        public string Summary { get; set; }

        [Field(IndexMode.NotIndexed)]
        public string ReleaseNotes { get; set; }

        public string Language { get; set; }

        [Field(IndexMode.NotIndexed)]
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

        [Field(IndexMode.NotIndexed)]
        public IEnumerable<string> Authors { get; set; }

        [Field(IndexMode.NotIndexed)]
        public IEnumerable<string> Owners { get; set; }

        [Field(IndexMode.NotIndexed, Converter = typeof(PackageDependencyConverter))]
        public IEnumerable<PackageDependency> Dependencies { get; set; }

        // TODO
        [IgnoreField]
        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies { get; set; }
        
        // TODO
        [IgnoreField]
        public IEnumerable<IPackageAssemblyReference> AssemblyReferences { get; set; }

        public IEnumerable<IPackageFile> GetFiles()
        {
            // todo: store but not index?
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

        [Field(IndexMode.NotAnalyzed, Converter = typeof(FrameworkNameConverter))]
        public IEnumerable<FrameworkName> SupportedFrameworks { get; set; }

        [IgnoreField]
        public string FullPath
        {
            get { return (Path != null) ? fileSystem.GetFullPath(Path) : null; }
        }

        #endregion
    }

    public class DateTimeOffsetToTicksConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(long);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(long);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            return new DateTimeOffset((long)value, TimeSpan.Zero);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (value is DateTime)
                return ((DateTime) value).ToUniversalTime().Ticks;

            return ((DateTimeOffset)value).Ticks;
        }
    }

    public class BoolToIntConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(int);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            return 1 == (int) value ? true : false;
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof (int);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            return ((bool) value) ? 1 : 0;
        }
    }

    public class FrameworkNameConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            return new FrameworkName((string) value);
        }
    }

    public class PackageDependencyConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof (string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var parts = ((string)value).Split(new[] { ':' }, 2);

            return new PackageDependency(parts[0], VersionUtility.ParseVersionSpec(parts[1]));
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            var d = (PackageDependency) value;
            return string.Format("{0}:{1}", d.Id, d.VersionSpec);
        }
    }
}