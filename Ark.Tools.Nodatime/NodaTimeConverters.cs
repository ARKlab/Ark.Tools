using NodaTime;
using NodaTime.Text;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace Ark.Tools.Nodatime
{
    public static class NodeTimeConverter
    {
        private static object _gate = new object();
        private static bool _registered = false;

        public static void Register()
        {
            lock (_gate)
            {
                if (!_registered)
                {
                    TypeDescriptor.AddAttributes(typeof(LocalDate), new System.ComponentModel.TypeConverterAttribute(typeof(LocalDateConverter)));
                    TypeDescriptor.AddAttributes(typeof(LocalTime), new System.ComponentModel.TypeConverterAttribute(typeof(LocalTimeConverter)));
                    TypeDescriptor.AddAttributes(typeof(LocalDateTime), new System.ComponentModel.TypeConverterAttribute(typeof(LocalDateTimeConverter)));
                    TypeDescriptor.AddAttributes(typeof(Instant), new System.ComponentModel.TypeConverterAttribute(typeof(InstantConverter)));
                    TypeDescriptor.AddAttributes(typeof(LocalDate?), new System.ComponentModel.TypeConverterAttribute(typeof(NullableLocalDateConverter)));
                    TypeDescriptor.AddAttributes(typeof(LocalTime?), new System.ComponentModel.TypeConverterAttribute(typeof(NullableLocalTimeConverter)));
                    TypeDescriptor.AddAttributes(typeof(LocalDateTime?), new System.ComponentModel.TypeConverterAttribute(typeof(NullableLocalDateTimeConverter)));
                    TypeDescriptor.AddAttributes(typeof(Instant?), new System.ComponentModel.TypeConverterAttribute(typeof(NullableInstantConverter)));
                    _registered = true;
                }
            }
        }
    }

    public class LocalDateConverter : TypeConverter
    {
        private readonly LocalDatePattern _pattern = LocalDatePattern.Iso;

        private static Type[] _supportedFrom = new[]
        {
            typeof(string),typeof(LocalDate),typeof(DateTime)
        };

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (_supportedFrom.Contains(sourceType)) return true;

            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            if (value == null) return null;

            if (value is LocalDate res)
            {
                return res;
            }
            if (value is DateTime d && d == d.Date)
            {
                return LocalDate.FromDateTime(d);
            }
            if (value is string s)
            {
                var r = _pattern.WithCulture(culture).Parse(s);
                if (r.Success)
                    return r.Value;
                // little hack, not the finest, but should work
                if (DateTime.TryParse(s, out var dt) && dt.Date == dt)
                    return LocalDate.FromDateTime(dt);
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
                return _pattern.Format((LocalDate)value);

            if (destinationType == typeof(DateTime))
                return ((LocalDate)value).ToDateTimeUnspecified();

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    public class NullableLocalDateConverter : NullableConverter
    {
        public NullableLocalDateConverter() : base(typeof(LocalDate?)) { }
    }

    public class LocalDateTimeConverter : TypeConverter
    {
        private readonly LocalDateTimePattern _pattern = LocalDateTimePattern.ExtendedIso;
        private static Type[] _supportedFrom = new[]
        {
            typeof(string),typeof(LocalDateTime),typeof(LocalDate),typeof(DateTime)
        };

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (_supportedFrom.Contains(sourceType)) return true;

            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            if (value is LocalDateTime ldt)
            {
                return ldt;
            }
            if (value is LocalDate ld)
            {
                return ld.AtMidnight();
            }
            if (value is DateTime d && d == d.Date)
            {
                return LocalDate.FromDateTime(d);
            }
            if (value is string s)
            {
                var r = _pattern.WithCulture(culture).Parse(s);
                if (r.Success)
                    return r.Value;
                // little hack, not the finest, but should work
                if (DateTime.TryParse(s, out var dt))
                    return LocalDateTime.FromDateTime(dt);
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
                return _pattern.Format((LocalDateTime)value);
            if (destinationType == typeof(DateTime))
                return ((LocalDateTime)value).ToDateTimeUnspecified();

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    public class NullableLocalDateTimeConverter : NullableConverter
    {
        public NullableLocalDateTimeConverter() : base(typeof(LocalDateTime?)) { }
    }

    public class InstantConverter : TypeConverter
    {
        private readonly InstantPattern _pattern = InstantPattern.ExtendedIso;
        private static Type[] _supportedFrom = new[]
        {
            typeof(string),typeof(Instant)
        };

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (_supportedFrom.Contains(sourceType)) return true;

            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            if (value is Instant i)
            {
                return i;
            }
            if (value is string s)
            {
                var r = _pattern.WithCulture(culture).Parse(s);
                if (r.Success)
                    return r.Value;
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
                return _pattern.Format((Instant)value);
            if (destinationType == typeof(DateTime))
                return ((Instant)value).ToDateTimeUtc();

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    public class NullableInstantConverter : NullableConverter
    {
        public NullableInstantConverter() : base(typeof(Instant?)) { }
    }


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

    public class NullableLocalTimeConverter : NullableConverter
    {
        public NullableLocalTimeConverter() : base(typeof(LocalTime?)) { }
    }
}
