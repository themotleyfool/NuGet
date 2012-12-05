using System.Collections.Generic;
using System.Linq;

namespace NuGet.Server.Infrastructure.Lucene
{
    public class IndexDifferences
    {
        private readonly IEnumerable<string> newPackages;
        private readonly IEnumerable<string> missingPackages;
        private readonly IEnumerable<string> modifiedPackages;

        public IndexDifferences(IEnumerable<string> newPackages, IEnumerable<string> missingPackages, IEnumerable<string> modifiedPackages)
        {
            this.newPackages = newPackages ?? new string[0];
            this.missingPackages = missingPackages ?? new string[0];
            this.modifiedPackages = modifiedPackages ?? new string[0];
        }

        /// <summary>
        /// Packages on the files system but not in the index.
        /// </summary>
        public IEnumerable<string> NewPackages
        {
            get { return newPackages; }
        }

        /// <summary>
        /// Packages in the index that are no longer found on the file system.
        /// </summary>
        public IEnumerable<string> MissingPackages
        {
            get { return missingPackages; }
        }

        /// <summary>
        /// Packages in the index and also in the file system where the modification timestamp is out of sync.
        /// </summary>
        public IEnumerable<string> ModifiedPackages
        {
            get { return modifiedPackages; }
        }

        /// <summary>
        /// Returns <code>true</code> when no differences are found.
        /// </summary>
        public bool IsEmpty
        {
            get { return NewPackages.Union(MissingPackages).Union(ModifiedPackages).IsEmpty(); }
        }
    }
}