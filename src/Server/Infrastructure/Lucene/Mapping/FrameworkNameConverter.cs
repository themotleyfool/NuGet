using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.Versioning;

namespace NuGet.Server.Infrastructure.Lucene.Mapping
{
    public class FrameworkNameConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            return new FrameworkName((string) value);
        }
    }
}