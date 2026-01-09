// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;
using NodaTime.Text;

using System.ComponentModel;
using System.Globalization;

namespace Ark.Tools.Nodatime;

public class LocalDateConverter : TypeConverter
{
    private readonly LocalDatePattern _pattern = LocalDatePattern.Iso;

    private static readonly Type[] _supportedFrom =
    [
        typeof(string),typeof(LocalDate),typeof(DateTime)
    ];

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        if (_supportedFrom.Contains(sourceType)) return true;

        return base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
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
            var r = _pattern.WithCulture(culture ?? CultureInfo.InvariantCulture).Parse(s);
            if (r.Success)
                return r.Value;
            // little hack, not the finest, but should work
            if (DateTime.TryParse(s, culture ?? CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) && dt.Date == dt)
                return LocalDate.FromDateTime(dt);
        }


        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        if (destinationType == typeof(string) || destinationType == typeof(DateTime))
            return true;

        return base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is LocalDate ld)
        {
            if (destinationType == typeof(string))
                return _pattern.Format(ld);

            if (destinationType == typeof(DateTime))
                return ld.ToDateTimeUnspecified();
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}