using NodaTime;
using NodaTime.Text;
using System;
using System.ComponentModel;
using System.Globalization;

namespace Ark.Tools.Nodatime
{
    public class LocalTimeConverter : TypeConverter
    {
        private readonly LocalTimePattern _pattern = LocalTimePattern.ExtendedIso;

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
                return true;

            if (sourceType == typeof(LocalTime))
                return true;

            if (sourceType == typeof(DateTime))
                return true;

            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            if (value is LocalTime lt)
            {
                return lt;
            }
            if (value is string s)
            {
                var res = _pattern.WithCulture(culture).Parse(s);
                if (res.Success)
                    return res.Value;
                // little hack, not the finest, but should work
                if (DateTime.TryParse(s, out var d))
                    return LocalTime.FromTicksSinceMidnight((d - d.Date).Ticks);
            }
            if (value is DateTime dt)
            {
                return LocalTime.FromTicksSinceMidnight((dt - dt.Date).Ticks);
            }

            return base.ConvertFrom(context, culture, value);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(string) || destinationType == typeof(DateTime))
                return true;

            return base.CanConvertTo(context, destinationType);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
                return _pattern.Format((LocalTime)value);
            if (destinationType == typeof(DateTime))
                return DateTime.MinValue.AddTicks(((LocalTime)value).TickOfDay);

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
