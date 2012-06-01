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

        public IEnumerable<string> NewPackages
        {
            get { return newPackages; }
        }

        public IEnumerable<string> MissingPackages
        {
            get { return missingPackages; }
        }

        public IEnumerable<string> ModifiedPackages
        {
            get { return modifiedPackages; }
        }

        public bool IsEmpty
        {
            get { return NewPackages.Union(MissingPackages).Union(ModifiedPackages).IsEmpty(); }
        }
    }
}