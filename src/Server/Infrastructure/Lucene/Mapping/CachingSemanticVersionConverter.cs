using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;

namespace NuGet.Server.Infrastructure.Lucene.Mapping
{
    public class CachingSemanticVersionConverter : SemanticVersionTypeConverter
    {
        private static readonly ReaderWriterLockSlim locks = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private static readonly IDictionary<string, SemanticVersion> cache = new Dictionary<string,SemanticVersion>();

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var stringValue = value as string;

            locks.EnterUpgradeableReadLock();

            try
            {
                SemanticVersion version;
                if (cache.TryGetValue(stringValue, out version))
                {
                    return version;
                }

                locks.EnterWriteLock();
                try
                {
                    version = (SemanticVersion) base.ConvertFrom(context, culture, value);
                    cache[stringValue] = version;
                    return version;
                }
                finally
                {
                    locks.ExitWriteLock();
                }
            }
            finally
            {
                locks.ExitUpgradeableReadLock();
            }
            
        }
    }
}