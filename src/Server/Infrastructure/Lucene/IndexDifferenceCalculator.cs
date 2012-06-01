using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Server.Infrastructure.Lucene
{
    public class IndexDifferenceCalculator
    {
        private readonly IFileSystem fileSystem;
        private readonly string[] fileSystemPackages;
        private readonly Dictionary<string, LucenePackage> indexedPackagesByPath;

        private IndexDifferenceCalculator(IFileSystem fileSystem, string[] fileSystemPackages, Dictionary<string, LucenePackage> indexedPackagesByPath)
        {
            this.fileSystem = fileSystem;
            this.fileSystemPackages = fileSystemPackages;
            this.indexedPackagesByPath = indexedPackagesByPath;
        }

        public static IndexDifferences FindDifferences(IFileSystem fileSystem, IEnumerable<LucenePackage> indexedPackages)
        {
            var fileSystemPackages = fileSystem.GetFiles(string.Empty, "*" + Constants.PackageExtension, true).ToArray();
            var indexedPackagesByPath = indexedPackages.ToDictionary(pkg => pkg.Path);

            var calc = new IndexDifferenceCalculator(fileSystem, fileSystemPackages, indexedPackagesByPath);

            return calc.Calculate();
        }

        private IndexDifferences Calculate()
        {
            var newPackages = Enumerable.Except(fileSystemPackages, indexedPackagesByPath.Keys);
            var missingPackages = Enumerable.Except(indexedPackagesByPath.Keys, fileSystemPackages);
            var modifiedPackages = fileSystemPackages.Intersect(indexedPackagesByPath.Keys).Where(ModifiedDateMismatch);

            return new IndexDifferences(newPackages, missingPackages, modifiedPackages);
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