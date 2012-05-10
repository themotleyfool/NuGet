using System;
using System.Collections.Generic;
using System.Data.Services.Common;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace NuGet
{
    [DataServiceKey("Id", "Version")]
    [EntityPropertyMapping("LastUpdated", SyndicationItemProperty.Updated, SyndicationTextContentKind.Plaintext, keepInContent: false)]
    [EntityPropertyMapping("Id", SyndicationItemProperty.Title, SyndicationTextContentKind.Plaintext, keepInContent: false)]
    [EntityPropertyMapping("Authors", SyndicationItemProperty.AuthorName, SyndicationTextContentKind.Plaintext, keepInContent: false)]
    [EntityPropertyMapping("Summary", SyndicationItemProperty.Summary, SyndicationTextContentKind.Plaintext, keepInContent: false)]
    [CLSCompliant(false)]
    public class DataServicePackage : IPackage
    {
        private IHashProvider _hashProvider;
        private bool _usingMachineCache;
        internal IPackage _package;

        public string Id
        {
            get;
            set;
        }

        public string Version
        {
            get;
            set;
        }

        public string Title
        {
            get;
            set;
        }

        public string Authors
        {
            get;
            set;
        }

        public string Owners
        {
            get;
            set;
        }

        public Uri IconUrl
        {
            get;
            set;
        }

        public Uri LicenseUrl
        {
            get;
            set;
        }

        public Uri ProjectUrl
        {
            get;
            set;
        }

        public Uri ReportAbuseUrl
        {
            get;
            set;
        }

        public Uri GalleryDetailsUrl
        {
            get;
            set;
        }

        public Uri DownloadUrl
        {
            get
            {
                return Context.GetReadStreamUri(this);
            }
        }

        public bool Listed
        {
            get;
            set;
        }

        public DateTimeOffset? Published
        {
            get;
            set;
        }

        public DateTimeOffset LastUpdated
        {
            get;
            set;
        }

        public int DownloadCount
        {
            get;
            set;
        }

        public int VersionDownloadCount
        {
            get;
            set;
        }

        public bool RequireLicenseAcceptance
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }

        public string Summary
        {
            get;
            set;
        }

        public string ReleaseNotes
        {
            get;
            set;
        }

        public string Language
        {
            get;
            set;
        }

        public string Tags
        {
            get;
            set;
        }

        public string Dependencies
        {
            get;
            set;
        }

        public string PackageHash
        {
            get;
            set;
        }

        public string PackageHashAlgorithm
        {
            get;
            set;
        }

        public bool IsLatestVersion
        {
            get;
            set;
        }

        public bool IsAbsoluteLatestVersion
        {
            get;
            set;
        }

        public string Copyright
        {
            get;
            set;
        }

        private string OldHash { get; set; }

        private IPackage Package
        {
            get
            {
                EnsurePackage(MachineCache.Default);
                return _package;
            }
        }

        internal IDataServiceContext Context
        {
            get;
            set;
        }

        internal PackageDownloader Downloader { get; set; }

        internal IHashProvider HashProvider
        {
            get { return _hashProvider ?? new CryptoHashProvider(PackageHashAlgorithm); }
            set { _hashProvider = value; }
        }

        bool IPackage.Listed
        {
            get
            {
                return Listed;
            }
        }

        IEnumerable<string> IPackageMetadata.Authors
        {
            get
            {
                if (String.IsNullOrEmpty(Authors))
                {
                    return Enumerable.Empty<string>();
                }
                return Authors.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        IEnumerable<string> IPackageMetadata.Owners
        {
            get
            {
                if (String.IsNullOrEmpty(Owners))
                {
                    return Enumerable.Empty<string>();
                }
                return Owners.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        IEnumerable<PackageDependency> IPackageMetadata.Dependencies
        {
            get
            {
                if (String.IsNullOrEmpty(Dependencies))
                {
                    return Enumerable.Empty<PackageDependency>();
                }
                return from d in Dependencies.Split('|')
                       let dependency = ParseDependency(d)
                       where dependency != null
                       select dependency;
            }
        }

        SemanticVersion IPackageMetadata.Version
        {
            get
            {
                if (Version != null)
                {
                    return new SemanticVersion(Version);
                }
                return null;
            }
        }

        public IEnumerable<IPackageAssemblyReference> AssemblyReferences
        {
            get
            {
                return Package.AssemblyReferences;
            }
        }

        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies
        {
            get
            {
                return Package.FrameworkAssemblies;
            }
        }

        public IEnumerable<IPackageFile> GetFiles()
        {
            return Package.GetFiles();
        }

        public Stream GetStream()
        {
            return Package.GetStream();
        }

        public override string ToString()
        {
            return this.GetFullName();
        }

        internal void EnsurePackage(IPackageRepository cacheRepository)
        {
            // OData caches instances of DataServicePackage while updating their property values. As a result, 
            // the ZipPackage that we downloaded may no longer be valid (as indicated by a newer hash). 
            // When using MachineCache, once we've verified that the hashes match (which happens the first time around),
            // we'll simply verify the file exists between successive calls.
            IPackageMetadata packageMetadata = this;
            bool refreshPackage = _package == null || 
                                  !String.Equals(OldHash, PackageHash, StringComparison.OrdinalIgnoreCase) || 
                                  (_usingMachineCache && !cacheRepository.Exists(Id, packageMetadata.Version));
            if (refreshPackage
                && TryGetPackage(cacheRepository, packageMetadata, out _package)
                && _package.GetHash(HashProvider).Equals(PackageHash, StringComparison.OrdinalIgnoreCase))
            {
                OldHash = PackageHash;

                // Reset the flag so that we no longer need to download the package since it exists and is valid.
                refreshPackage = false;

                // Make a note that we the backing store for the ZipPackage is the machine cache.
                _usingMachineCache = true;
            }

            if (refreshPackage)
            {
                // We either do not have a package available locally or they are invalid. Download the package from the server.
                _package = Downloader.DownloadPackage(DownloadUrl, this);

                // Make a note that we are using an in-memory instance of the package.
                _usingMachineCache = false;

                // Add the package to the cache
                cacheRepository.AddPackage(_package);

                OldHash = PackageHash;

                // Clear any cached items for this package
                ZipPackage.ClearCache(_package);
            }
        }

        /// <summary>
        /// Parses a dependency from the feed in the format:
        /// id:versionSpec or id
        /// </summary>
        private static PackageDependency ParseDependency(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string[] tokens = value.Trim().Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0)
            {
                return null;
            }

            // Trim the id
            string id = tokens[0].Trim();
            IVersionSpec versionSpec = null;

            if (tokens.Length > 1)
            {
                // Attempt to parse the version
                VersionUtility.TryParseVersionSpec(tokens[1], out versionSpec);
            }

            return new PackageDependency(id, versionSpec);
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to return null if any error occurred while trying to find the package.")]
        private static bool TryGetPackage(IPackageRepository repository, IPackageMetadata packageMetadata, out IPackage package)
        {
            try
            {
                package = repository.FindPackage(packageMetadata.Id, packageMetadata.Version);
            }
            catch
            {
                // If the package in the repository is corrupted then return null
                package = null;
            }
            return package != null;
        }
    }
}