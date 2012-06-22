using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace NuGet.Server.Infrastructure.Lucene.Mapping
{
    public static class PackageDependencySetConverter
    {
        public static IEnumerable<string> Flatten(PackageDependencySet set)
        {
            var shortFrameworkName = set.TargetFramework == null ? null : VersionUtility.GetShortFrameworkName(set.TargetFramework);

            if (shortFrameworkName != null && set.Dependencies.Count == 0)
            {
                return new[] {"::" + shortFrameworkName};
            }

            return set.Dependencies.Select(d => string.Format("{0}:{1}:{2}", d.Id, d.VersionSpec, shortFrameworkName));
        }

        public static IEnumerable<PackageDependencySet> Parse(IEnumerable<string> dependencies)
        {
            var map = new Dictionary<string, List<PackageDependency>>();

            foreach (var str in dependencies)
            {
                var parts = str.Split(':');
                var key = parts.Length >= 3 ? parts[2] : string.Empty;

                List<PackageDependency> set;
                if (!map.TryGetValue(key, out set))
                {
                    set = new List<PackageDependency>();
                    map[key] = set;
                }

                var id = parts[0];
                var version = parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1])
                                  ? VersionUtility.ParseVersionSpec(parts[1])
                                  : null;

                if (string.IsNullOrWhiteSpace(id)) continue;

                set.Add(new PackageDependency(id, version));
            }

            return map.Select(kv => new PackageDependencySet(ParseFrameworkName(kv.Key), kv.Value));
        }

        private static FrameworkName ParseFrameworkName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            // cache?
            return VersionUtility.ParseFrameworkName(name);
        }
    }
}