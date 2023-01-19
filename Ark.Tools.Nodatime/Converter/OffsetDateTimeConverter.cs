// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;
using NodaTime.Text;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace Ark.Tools.Nodatime
{
    public class OffsetDateTimeConverter : TypeConverter
    {
		private readonly OffsetDateTimePattern _pattern = OffsetDateTimePattern.ExtendedIso;
		private static Type[] _supportedFrom = new[]
		{
			typeof(string),typeof(OffsetDateTime),typeof(DateTimeOffset)
		};

		public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
		{
			if (_supportedFrom.Contains(sourceType)) return true;

			return base.CanConvertFrom(context, sourceType);
		}

		public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
		{
			if (value is OffsetDateTime i)
			{
				return i;
			}
			if (value is DateTimeOffset dto)
			{
				return OffsetDateTime.FromDateTimeOffset(dto);
			}
			if (value is string s)
			{
				var r = _pattern.WithCulture(culture ?? CultureInfo.InvariantCulture).Parse(s);
				if (r.Success)
					return r.Value;
			}

			return base.ConvertFrom(context, culture, value);
		}

		public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
		{
			if (destinationType == typeof(string) || destinationType == typeof(DateTimeOffset))
				return true;

			return base.CanConvertTo(context, destinationType);
		}

		public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
		{
            if (value is OffsetDateTime odt)
            {
                if (destinationType == typeof(string))
                    return _pattern.Format(odt);
                if (destinationType == typeof(DateTimeOffset))
                    return odt.ToDateTimeOffset();
            }

			return base.ConvertTo(context, culture, value, destinationType);
		}
	}
}
