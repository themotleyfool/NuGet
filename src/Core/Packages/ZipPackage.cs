using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using NuGet.Resources;

namespace NuGet
{
    public class ZipPackage : LocalPackage
    {
        private const string CacheKeyFormat = "NUGET_ZIP_PACKAGE_{0}_{1}{2}";
        private const string AssembliesCacheKey = "ASSEMBLIES";
        private const string FilesCacheKey = "FILES";

        private readonly bool _enableCaching;
          
        private static readonly TimeSpan CacheTimeout = TimeSpan.FromSeconds(15);

        // paths to exclude
        private static readonly string[] ExcludePaths = new[] { "_rels", "package" };

        // We don't store the stream itself, just a way to open the stream on demand
        // so we don't have to hold on to that resource
        private readonly Func<Stream> _streamFactory;

        public ZipPackage(string fileName)
            : this(fileName, enableCaching: false)
        {
        }

        public ZipPackage(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            _enableCaching = false;
            _streamFactory = stream.ToStreamFactory();
            EnsureManifest();
        }

        internal ZipPackage(string fileName, bool enableCaching)
        {
            if (String.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "fileName");
            }
            _enableCaching = enableCaching;
            _streamFactory = () => File.OpenRead(fileName);
            EnsureManifest();
        }

        internal ZipPackage(Func<Stream> streamFactory, bool enableCaching)
        {
            if (streamFactory == null)
            {
                throw new ArgumentNullException("streamFactory");
            }
            _enableCaching = enableCaching;
            _streamFactory = streamFactory;
            EnsureManifest();
        }

        protected override IEnumerable<IPackageAssemblyReference> GetAssemblyReferencesBase()
        {
            if (_enableCaching)
            {
                return MemoryCache.Instance.GetOrAdd(GetAssembliesCacheKey(), GetAssembliesNoCache, CacheTimeout);
            }
            return GetAssembliesNoCache();
        }

        protected override IEnumerable<IPackageFile> GetFilesBase()
        {
            if (_enableCaching)
            {
                return MemoryCache.Instance.GetOrAdd(GetFilesCacheKey(), GetFilesNoCache, CacheTimeout);
            }
            return GetFilesNoCache();
        }

        public override Stream GetStream()
        {
            return _streamFactory();
        }

        private List<IPackageAssemblyReference> GetAssembliesNoCache()
        {
            return (from file in GetFiles()
                    where IsAssemblyReference(file)
                    select (IPackageAssemblyReference)new ZipPackageAssemblyReference(file)).ToList();
        }

        private List<IPackageFile> GetFilesNoCache()
        {
            using (Stream stream = _streamFactory())
            {
                Package package = Package.Open(stream);

                return (from part in package.GetParts()
                        where IsPackageFile(part)
                        select (IPackageFile)new ZipPackageFile(part)).ToList();
            }
        }

        private void EnsureManifest()
        {
            using (Stream stream = _streamFactory())
            {
                Package package = Package.Open(stream);

                PackageRelationship relationshipType = package.GetRelationshipsByType(Constants.PackageRelationshipNamespace + PackageBuilder.ManifestRelationType).SingleOrDefault();

                if (relationshipType == null)
                {
                    throw new InvalidOperationException(NuGetResources.PackageDoesNotContainManifest);
                }

                PackagePart manifestPart = package.GetPart(relationshipType.TargetUri);

                if (manifestPart == null)
                {
                    throw new InvalidOperationException(NuGetResources.PackageDoesNotContainManifest);
                }

                using (Stream manifestStream = manifestPart.GetStream())
                {
                    ReadManifest(manifestStream);
                }
            }
        }

        private string GetFilesCacheKey()
        {
            return String.Format(CultureInfo.InvariantCulture, CacheKeyFormat, FilesCacheKey, Id, Version);
        }

        private string GetAssembliesCacheKey()
        {
            return String.Format(CultureInfo.InvariantCulture, CacheKeyFormat, AssembliesCacheKey, Id, Version);
        }

        protected static bool IsPackageFile(PackagePart part)
        {
            string path = UriUtility.GetPath(part.Uri);
            // We exclude any opc files and the manifest file (.nuspec)
            return !ExcludePaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) &&
                   !PackageUtility.IsManifest(path);
        }

        internal static void ClearCache(IPackage package)
        {
            var zipPackage = package as ZipPackage;

            // Remove the cache entries for files and assemblies
            if (zipPackage != null)
            {
                MemoryCache.Instance.Remove(zipPackage.GetAssembliesCacheKey());
                MemoryCache.Instance.Remove(zipPackage.GetFilesCacheKey());
            }
        }
    }
}
