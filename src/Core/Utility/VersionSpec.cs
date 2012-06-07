using System.Globalization;
using System.Text;

namespace NuGet
{
    public class VersionSpec : IVersionSpec
    {
        public VersionSpec()
        {
        }

        public VersionSpec(SemanticVersion version)
        {
            IsMinInclusive = true;
            IsMaxInclusive = true;
            MinVersion = version;
            MaxVersion = version;
        }

        public SemanticVersion MinVersion { get; set; }
        public bool IsMinInclusive { get; set; }
        public SemanticVersion MaxVersion { get; set; }
        public bool IsMaxInclusive { get; set; }

        public override string ToString()
        {
            if (MinVersion != null && IsMinInclusive && MaxVersion == null && !IsMaxInclusive)
            {
                return MinVersion.ToString();
            }

            if (MinVersion != null && MaxVersion != null && MinVersion == MaxVersion && IsMinInclusive && IsMaxInclusive)
            {
                return "[" + MinVersion + "]";
            }

            var versionBuilder = new StringBuilder();
            versionBuilder.Append(IsMinInclusive ? '[' : '(');
            versionBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0}, {1}", MinVersion, MaxVersion);
            versionBuilder.Append(IsMaxInclusive ? ']' : ')');

            return versionBuilder.ToString();
        }

        public bool Equals(VersionSpec other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.MinVersion, MinVersion) && other.IsMinInclusive.Equals(IsMinInclusive) && Equals(other.MaxVersion, MaxVersion) && other.IsMaxInclusive.Equals(IsMaxInclusive);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (VersionSpec)) return false;
            return Equals((VersionSpec) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = (MinVersion != null ? MinVersion.GetHashCode() : 0);
                result = (result*397) ^ IsMinInclusive.GetHashCode();
                result = (result*397) ^ (MaxVersion != null ? MaxVersion.GetHashCode() : 0);
                result = (result*397) ^ IsMaxInclusive.GetHashCode();
                return result;
            }
        }

        public static bool operator ==(VersionSpec left, VersionSpec right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(VersionSpec left, VersionSpec right)
        {
            return !Equals(left, right);
        }
    }
}