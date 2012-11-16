using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;

namespace NuGet.Server.Infrastructure.Lucene.Mapping
{
    public class CachingSemanticVersionConverter : SemanticVersionTypeConverter
    {
        private static readonly ReaderWriterLockSlim locks = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private static readonly IDictionary<string, StrictSemanticVersion> cache = new Dictionary<string, StrictSemanticVersion>();

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var stringValue = value as string;

            locks.EnterUpgradeableReadLock();

            try
            {
                StrictSemanticVersion version;
                if (cache.TryGetValue(stringValue, out version))
                {
                    return version;
                }

                locks.EnterWriteLock();
                try
                {
                    var baseVersion = base.ConvertFrom(context, culture, value);
                    version = baseVersion != null ? new StrictSemanticVersion(baseVersion.ToString()) : null;
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