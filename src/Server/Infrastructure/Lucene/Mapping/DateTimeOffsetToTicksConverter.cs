using System;
using System.ComponentModel;
using System.Globalization;

namespace NuGet.Server.Infrastructure.Lucene.Mapping
{
    public class DateTimeOffsetToTicksConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(long);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(long);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            return new DateTimeOffset((long)value, TimeSpan.Zero);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (value is DateTime)
                return ((DateTime) value).ToUniversalTime().Ticks;

            return ((DateTimeOffset)value).Ticks;
        }
    }
}