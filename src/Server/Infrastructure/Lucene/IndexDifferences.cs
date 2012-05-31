using System;
using System.Linq;
using System.Collections.Generic;

namespace NuGet.Server.Infrastructure.Lucene
{
    public class IndexDifferences
    {
        private readonly IFileSystem fileSystem;
        private readonly string[] fileSystemPackages;
        private readonly Dictionary<string, LucenePackage> indexedPackagesByPath;

        public IEnumerable<string> MissingPackages { get; private set; }
        public IEnumerable<string> NewPackages { get; private set; }
        public IEnumerable<string> ModifiedPackages { get; private set; }

        private IndexDifferences(IFileSystem fileSystem, string[] fileSystemPackages, Dictionary<string, LucenePackage> indexedPackagesByPath)
        {
            this.fileSystem = fileSystem;
            this.fileSystemPackages = fileSystemPackages;
            this.indexedPackagesByPath = indexedPackagesByPath;
            MissingPackages = new string[0];
            NewPackages = new string[0];
            ModifiedPackages = new string[0];
        }

        public static IndexDifferences FindDifferences(IFileSystem fileSystem, IEnumerable<LucenePackage> indexedPackages)
        {
            var fileSystemPackages = fileSystem.GetFiles(string.Empty, "*" + Constants.PackageExtension, true).ToArray();
            var indexedPackagesByPath = indexedPackages.ToDictionary(pkg => pkg.Path);

            var diff = new IndexDifferences(fileSystem, fileSystemPackages, indexedPackagesByPath);

            diff.Calculate();

            return diff;
        }

        private void Calculate()
        {
            NewPackages = Enumerable.Except(fileSystemPackages, indexedPackagesByPath.Keys);
            MissingPackages = Enumerable.Except(indexedPackagesByPath.Keys, fileSystemPackages);
            ModifiedPackages = fileSystemPackages.Intersect(indexedPackagesByPath.Keys).Where(ModifiedDateMismatch);
        }

        private bool ModifiedDateMismatch(string path)
        {
            var lucenePackage = indexedPackagesByPath[path];

            if (!lucenePackage.Published.HasValue)
            {
                return true;
            }

            var diff = fileSystem.GetLastModified(path) - lucenePackage.Published.Value;
            return Math.Abs(diff.TotalSeconds) > 1;
        }
    }
}