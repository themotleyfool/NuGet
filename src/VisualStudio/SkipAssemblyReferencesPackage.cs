﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.VisualStudio
{
    internal class SkipAssemblyReferencesPackage : IPackage
    {
        private readonly IPackage _basePackage;

        public SkipAssemblyReferencesPackage(IPackage basePackage)
        {
            if (basePackage == null)
            {
                throw new ArgumentNullException("basePackage");
            }
            _basePackage = basePackage;
        }

        public bool IsAbsoluteLatestVersion
        {
            get { return _basePackage.IsAbsoluteLatestVersion; }
        }

        public bool IsLatestVersion
        {
            get { return _basePackage.IsLatestVersion; }
        }

        public bool Listed
        {
            get { return _basePackage.Listed; }
        }

        public DateTimeOffset? Published
        {
            get { return _basePackage.Published; }
        }

        public IEnumerable<IPackageAssemblyReference> AssemblyReferences
        {
            get 
            {
                return Enumerable.Empty<IPackageAssemblyReference>(); 
            }
        }

        public IEnumerable<IPackageFile> GetFiles()
        {
            return _basePackage.GetFiles();
        }

        public System.IO.Stream GetStream()
        {
            return _basePackage.GetStream();
        }

        public string Id
        {
            get { return _basePackage.Id; }
        }

        public SemanticVersion Version
        {
            get { return _basePackage.Version; }
        }

        public string Title
        {
            get { return _basePackage.Title; }
        }

        public IEnumerable<string> Authors
        {
            get { return _basePackage.Authors; }
        }

        public IEnumerable<string> Owners
        {
            get { return _basePackage.Owners; }
        }

        public Uri IconUrl
        {
            get { return _basePackage.IconUrl; }
        }

        public Uri LicenseUrl
        {
            get { return _basePackage.LicenseUrl; }
        }

        public Uri ProjectUrl
        {
            get { return _basePackage.ProjectUrl; }
        }

        public bool RequireLicenseAcceptance
        {
            get { return _basePackage.RequireLicenseAcceptance; }
        }

        public string Description
        {
            get { return _basePackage.Description; }
        }

        public string Summary
        {
            get { return _basePackage.Summary; }
        }

        public string ReleaseNotes
        {
            get { return _basePackage.ReleaseNotes; }
        }

        public string Language
        {
            get { return _basePackage.Language; }
        }

        public string Tags
        {
            get { return _basePackage.Tags; }
        }

        public string Copyright
        {
            get { return _basePackage.Copyright; }
        }

        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies
        {
            get { return Enumerable.Empty<FrameworkAssemblyReference>(); }
        }

        public IEnumerable<PackageDependency> Dependencies
        {
            get { return _basePackage.Dependencies; }
        }

        public Uri ReportAbuseUrl
        {
            get { return _basePackage.ReportAbuseUrl; }
        }

        public int DownloadCount
        {
            get { return _basePackage.DownloadCount; }
        }

        public int VersionDownloadCount
        {
            get { return _basePackage.VersionDownloadCount; }
        }
    }
}
