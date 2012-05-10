using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace NuGet
{
    public class LocalPackageRepository : PackageRepositoryBase, IPackageLookup
    {
        private readonly ConcurrentDictionary<string, PackageCacheEntry> _packageCache = new ConcurrentDictionary<string, PackageCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<PackageName, string> _packagePathLookup = new ConcurrentDictionary<PackageName, string>();
        private readonly bool _enableCaching;

        public LocalPackageRepository(string physicalPath)
            : this(physicalPath, enableCaching: true)
        {
        }

        public LocalPackageRepository(string physicalPath, bool enableCaching)
            : this(new DefaultPackagePathResolver(physicalPath),
                   new PhysicalFileSystem(physicalPath),
                   enableCaching)
        {
        }

        public LocalPackageRepository(IPackagePathResolver pathResolver, IFileSystem fileSystem)
            : this(pathResolver, fileSystem, enableCaching: true)
        {
        }

        public LocalPackageRepository(IPackagePathResolver pathResolver, IFileSystem fileSystem, bool enableCaching)
        {
            if (pathResolver == null)
            {
                throw new ArgumentNullException("pathResolver");
            }

            if (fileSystem == null)
            {
                throw new ArgumentNullException("fileSystem");
            }

            FileSystem = fileSystem;
            PathResolver = pathResolver;
            _enableCaching = enableCaching;
        }

        public override string Source
        {
            get
            {
                return FileSystem.Root;
            }
        }

        public override bool SupportsPrereleasePackages
        {
            get { return true; }
        }

        protected IFileSystem FileSystem
        {
            get;
            private set;
        }

        public IPackagePathResolver PathResolver
        {
            get;
            set;
        }

        public override IQueryable<IPackage> GetPackages()
        {
            return GetPackages(OpenPackage).AsQueryable();
        }

        public override void AddPackage(IPackage package)
        {
            string packageFilePath = GetPackageFilePath(package);

            FileSystem.AddFileWithCheck(packageFilePath, package.GetStream);
        }

        public override void RemovePackage(IPackage package)
        {
            // Delete the package file
            string packageFilePath = GetPackageFilePath(package);
            FileSystem.DeleteFileSafe(packageFilePath);

            // Delete the package directory if any
            FileSystem.DeleteDirectorySafe(PathResolver.GetPackageDirectory(package), recursive: false);

            // If this is the last package delete the package directory
            if (!FileSystem.GetFilesSafe(String.Empty).Any() &&
                !FileSystem.GetDirectoriesSafe(String.Empty).Any())
            {
                FileSystem.DeleteDirectorySafe(String.Empty, recursive: false);
            }
        }

        public virtual IPackage FindPackage(string packageId, SemanticVersion version)
        {
            return FindPackage(OpenPackage, packageId, version);
        }

        public virtual bool Exists(string packageId, SemanticVersion version)
        {
            return FindPackage(packageId, version) != null;
        }

        public IEnumerable<string> GetPackageLookupPaths(string packageId, SemanticVersion version)
        {
            // Files created by the path resolver. This would take into account the non-side-by-side scenario 
            // and we do not need to match this for id and version.
            var packageFileName = PathResolver.GetPackageFileName(packageId, version);
            var filesMatchingFullName = GetPackageFiles(packageFileName);

            if (version.Version.Revision < 1)
            {
                // If the build or revision number is not set, we need to look for combinations of the format
                // * Foo.1.2.nupkg
                // * Foo.1.2.3.nupkg
                // * Foo.1.2.0.nupkg
                // * Foo.1.2.0.0.nupkg
                // To achieve this, we would look for files named 1.2*.nupkg if both build and revision are 0 and
                // 1.2.3*.nupkg if only the revision is set to 0.
                string partialName = version.Version.Build < 1 ?
                                        String.Join(".", packageId, version.Version.Major, version.Version.Minor) :
                                        String.Join(".", packageId, version.Version.Major, version.Version.Minor, version.Version.Build);
                partialName += "*" + Constants.PackageExtension;

                // Partial names would result is gathering package with matching major and minor but different build and revision. 
                // Attempt to match the version in the path to the version we're interested in.
                var partialNameMatches = GetPackageFiles(partialName).Where(path => FileNameMatchesPattern(packageId, version, path));
                return Enumerable.Concat(filesMatchingFullName, partialNameMatches);
            }
            return filesMatchingFullName;
        }

        internal IPackage FindPackage(Func<string, IPackage> openPackage, string packageId, SemanticVersion version)
        {
            var lookupPackageName = new PackageName(packageId, version);
            string packagePath;
            if (_packagePathLookup.TryGetValue(lookupPackageName, out packagePath))
            {
                return GetPackage(openPackage, packagePath);
            }

            // Lookup files which start with the name "<Id>." and attempt to match it with all possible version string combinations (e.g. 1.2.0, 1.2.0.0) 
            // before opening the package. To avoid creating file name strings, we attempt to specifically match everything after the last path separator
            // which would be the file name and extension.
            return (from path in GetPackageLookupPaths(packageId, version)
                    let package = GetPackage(openPackage, path)
                    where lookupPackageName.Equals(new PackageName(package.Id, package.Version))
                    select package).FirstOrDefault();
        }

        internal IEnumerable<IPackage> GetPackages(Func<string, IPackage> openPackage)
        {
            return from path in GetPackageFiles()
                   select GetPackage(openPackage, path);
        }

        private IPackage GetPackage(Func<string, IPackage> openPackage, string path)
        {
            PackageCacheEntry cacheEntry;
            DateTimeOffset lastModified = FileSystem.GetLastModified(path);
            // If we never cached this file or we did and it's current last modified time is newer
            // create a new entry
            if (!_packageCache.TryGetValue(path, out cacheEntry) ||
                (cacheEntry != null && lastModified > cacheEntry.LastModifiedTime))
            {
                // We need to do this so we capture the correct loop variable
                string packagePath = path;

                // Create the package
                IPackage package = openPackage(packagePath);


                // create a cache entry with the last modified time
                cacheEntry = new PackageCacheEntry(package, lastModified);

                if (_enableCaching)
                {
                    // Store the entry
                    _packageCache[packagePath] = cacheEntry;
                    _packagePathLookup[new PackageName(package.Id, package.Version)] = path;
                }
            }

            return cacheEntry.Package;
        }

        internal IEnumerable<string> GetPackageFiles(string filter = null)
        {
            filter = filter ?? "*" + Constants.PackageExtension;
            Debug.Assert(filter.EndsWith(Constants.PackageExtension, StringComparison.OrdinalIgnoreCase));

            // Check for package files one level deep. We use this at package install time
            // to determine the set of installed packages. Installed packages are copied to 
            // {id}.{version}\{packagefile}.{extension}.
            foreach (var dir in FileSystem.GetDirectories(String.Empty))
            {
                foreach (var path in FileSystem.GetFiles(dir, filter))
                {
                    yield return path;
                }
            }

            // Check top level directory
            foreach (var path in FileSystem.GetFiles(String.Empty, filter))
            {
                yield return path;
            }
        }

        protected virtual IPackage OpenPackage(string path)
        {
            var package = new ZipPackage(() => FileSystem.OpenFile(path), _enableCaching);

            // Set the last modified date on the package
            package.Published = FileSystem.GetLastModified(path);

            // Clear the cache whenever we open a new package file
            ZipPackage.ClearCache(package);
            return package;
        }

        protected virtual string GetPackageFilePath(IPackage package)
        {
            return Path.Combine(PathResolver.GetPackageDirectory(package),
                                PathResolver.GetPackageFileName(package));
        }

        protected virtual string GetPackageFilePath(string id, SemanticVersion version)
        {
            return Path.Combine(PathResolver.GetPackageDirectory(id, version),
                                PathResolver.GetPackageFileName(id, version));
        }

        private static bool FileNameMatchesPattern(string packageId, SemanticVersion version, string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            SemanticVersion parsedVersion;
            
            // When matching by pattern, we will always have a version token. Packages without versions would be matched early on by the version-less path resolver 
            // when doing an exact match.
            return name.Length > packageId.Length &&
                   SemanticVersion.TryParse(name.Substring(packageId.Length + 1), out parsedVersion) &&
                   parsedVersion == version;
        }

        private class PackageCacheEntry
        {
            public PackageCacheEntry(IPackage package, DateTimeOffset lastModifiedTime)
            {
                Package = package;
                LastModifiedTime = lastModifiedTime;
            }

            public IPackage Package { get; private set; }
            public DateTimeOffset LastModifiedTime { get; private set; }
        }
    }
}