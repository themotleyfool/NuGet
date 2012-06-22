using System;
using System.ComponentModel;
using System.Globalization;

namespace NuGet.Server.Infrastructure.Lucene.Mapping
{
    public class BoolToIntConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(int);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            return 1 == (int) value ? true : false;
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof (int);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            return ((bool) value) ? 1 : 0;
        }
    }
}