using NodaTime;
using NodaTime.Text;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Reqnroll.Assist;

namespace Ark.Tools.Reqnroll
{
    public class NodaTimeValueRetriverAndComparer : IValueRetriever, IValueComparer
    {
        private Type[] _types = [typeof(LocalDate), typeof(LocalDateTime), typeof(Instant), typeof(LocalTime), typeof(OffsetDateTime), typeof(ZonedDateTime)];
        private Type[] _nullableTypes = [typeof(LocalDate?), typeof(LocalDateTime?), typeof(Instant?), typeof(LocalTime?), typeof(OffsetDateTime?), typeof(ZonedDateTime)];

        public bool CanCompare(object actualValue)
        {
            return _types.Contains(actualValue?.GetType());
        }

        public bool CanRetrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
        {
            return _types.Contains(propertyType) || _nullableTypes.Contains(propertyType);
        }

        public bool Compare(string expectedValue, object actualValue)
        {

            switch (actualValue)
            {
                case LocalDate ld:
                    {
                        var res1 = LocalDatePattern.Iso.Parse(expectedValue);
                        if (res1.Success) return res1.Value == ld;

                        var res4 = LocalDateTimePattern.ExtendedIso.Parse(expectedValue);
                        if (res4.Success && res4.Value.TimeOfDay == LocalTime.Midnight) return res4.Value.Date == ld;

                        if (DateTime.TryParse(expectedValue, CultureInfo.CurrentCulture, DateTimeStyles.None, out var d) && d == d.Date)
                        {
                            return LocalDate.FromDateTime(d) == ld;
                        }
                        return false;
                    }
                case LocalDateTime ldt:
                    {
                        var res2 = LocalDateTimePattern.ExtendedIso.Parse(expectedValue);
                        if (res2.Success) return res2.Value == ldt;

                        if (DateTime.TryParse(expectedValue, CultureInfo.CurrentCulture, DateTimeStyles.None, out var d))
                        {
                            return LocalDateTime.FromDateTime(d) == ldt;
                        }
                        return false;
                    }
                case Instant i:
                    {
                        var res3 = InstantPattern.ExtendedIso.Parse(expectedValue);
                        if (res3.Success) return res3.Value == i;

                        if (DateTime.TryParse(expectedValue, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d))
                        {
                            return Instant.FromDateTimeUtc(d) == i;
                        }

                        return false;
                    }
                case LocalTime t:
                    {
                        var res5 = LocalTimePattern.ExtendedIso.Parse(expectedValue);
                        if (!res5.Success) return false;
                        return res5.Value == t;
                    }
                case OffsetDateTime odt:
                    {
                        var res6 = OffsetDateTimePattern.ExtendedIso.Parse(expectedValue);
                        if (res6.Success) return res6.Value == odt;

                        if (DateTimeOffset.TryParse(expectedValue, CultureInfo.CurrentCulture, DateTimeStyles.None, out var o))
                        {
                            return OffsetDateTime.FromDateTimeOffset(o) == odt;
                        }

                        return false;
                    }
                case ZonedDateTime zdt:
                    {
                        var res7 = ZonedDateTimePattern.ExtendedFormatOnlyIso.WithZoneProvider(DateTimeZoneProviders.Tzdb).Parse(expectedValue);
                        if (res7.Success) return res7.Value == zdt;

                        return false;
                    }
            }

            return false;
        }

        public object? Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
        {
            var t = Nullable.GetUnderlyingType(propertyType);
            if (t != null)
            {
                if (string.IsNullOrWhiteSpace(keyValuePair.Value)) return null;
            }
            else t = propertyType;

            if (t == typeof(LocalDate))
            {
                var res = LocalDatePattern.Iso.Parse(keyValuePair.Value);
                if (res.Success) return res.Value;

                var res4 = LocalDateTimePattern.ExtendedIso.Parse(keyValuePair.Value);
                if (res4.Success && res4.Value.TimeOfDay == LocalTime.Midnight) return res4.Value.Date;


                if (DateTime.TryParse(keyValuePair.Value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var d) && d == d.Date)
                {
                    return LocalDate.FromDateTime(d);
                }

                throw _getInvalidOperationException(keyValuePair.Value);
            }

            if (t == typeof(LocalDateTime))
            {
                var res = LocalDateTimePattern.ExtendedIso.Parse(keyValuePair.Value);
                if (res.Success) return res.Value;

                if (DateTime.TryParse(keyValuePair.Value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var d))
                {
                    return LocalDateTime.FromDateTime(d);
                }

                throw _getInvalidOperationException(keyValuePair.Value);
            }

            if (t == typeof(Instant))
            {
                var res = InstantPattern.ExtendedIso.Parse(keyValuePair.Value);
                if (res.Success) return res.Value;

                if (DateTime.TryParse(keyValuePair.Value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d))
                {
                    return Instant.FromDateTimeUtc(d);
                }

                throw _getInvalidOperationException(keyValuePair.Value);
            }

            if (t == typeof(LocalTime))
            {
                var res = LocalTimePattern.ExtendedIso.Parse(keyValuePair.Value);
                if (!res.Success) throw _getInvalidOperationException(keyValuePair.Value);
                return res.Value;
            }

            if (t == typeof(OffsetDateTime))
            {
                var res = OffsetDateTimePattern.ExtendedIso.Parse(keyValuePair.Value);
                if (res.Success) return res.Value;

                if (DateTimeOffset.TryParse(keyValuePair.Value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var o))
                {
                    return OffsetDateTime.FromDateTimeOffset(o);
                }

                throw _getInvalidOperationException(keyValuePair.Value);
            }

            if (t == typeof(ZonedDateTime))
            {
                var res = ZonedDateTimePattern.ExtendedFormatOnlyIso.WithZoneProvider(DateTimeZoneProviders.Tzdb).Parse(keyValuePair.Value);
                if (!res.Success) throw _getInvalidOperationException(keyValuePair.Value);
                return res.Value;
            }

            throw new NotSupportedException();
        }

        private static InvalidOperationException _getInvalidOperationException(string value) => new($"Cannot parse value {value} pattern");
    }
}
