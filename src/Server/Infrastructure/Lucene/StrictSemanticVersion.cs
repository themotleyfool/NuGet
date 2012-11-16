using System;

namespace NuGet.Server.Infrastructure.Lucene
{
    public class StrictSemanticVersion : SemanticVersion, IEquatable<StrictSemanticVersion>
    {
        public StrictSemanticVersion(string version) : base(version)
        {
        }

        public bool Equals(StrictSemanticVersion other)
        {
            return ReferenceEquals(this, other) || (other != null && base.ToString().Equals(other.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || Equals(obj as StrictSemanticVersion);
        }

        public override int GetHashCode()
        {
            return base.ToString().ToLowerInvariant().GetHashCode();
        }
    }
}