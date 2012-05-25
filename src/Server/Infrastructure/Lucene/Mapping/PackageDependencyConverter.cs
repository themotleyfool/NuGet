using System;
using System.ComponentModel;
using System.Globalization;

namespace NuGet.Server.Infrastructure.Lucene.Mapping
{
    public class PackageDependencyConverter : TypeConverter
    {
        public const string AnyVersionSpec = "[0.0.0.0,65535.65535.65535.65535]";

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof (string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var parts = ((string)value).Split(new[] { ':' }, 2);
            var spec = string.IsNullOrEmpty(parts[1]) ? AnyVersionSpec : parts[1];

            return new PackageDependency(parts[0], VersionUtility.ParseVersionSpec(spec));
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            var d = (PackageDependency) value;
            return string.Format("{0}:{1}", d.Id, d.VersionSpec);
        }
    }
}