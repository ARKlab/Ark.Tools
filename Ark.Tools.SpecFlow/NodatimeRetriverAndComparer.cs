using NodaTime;
using NodaTime.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using TechTalk.SpecFlow.Assist;

namespace Ark.Tools.SpecFlow
{
    public class NodaTimeValueRetriverAndComparer : IValueRetriever, IValueComparer
    {
        private Type[] _types = new[] { typeof(LocalDate), typeof(LocalDateTime), typeof(Instant), typeof(LocalTime), typeof(OffsetDateTime) };
        private Type[] _nullableTypes = new[] { typeof(LocalDate?), typeof(LocalDateTime?), typeof(Instant?), typeof(LocalTime?), typeof(OffsetDateTime?) };

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
                    var res1 = LocalDatePattern.Iso.Parse(expectedValue);
                    if (res1.Success) return res1.Value == ld;

                    var res4 = LocalDateTimePattern.ExtendedIso.Parse(expectedValue);
                    if (res4.Success && res4.Value.TimeOfDay == LocalTime.Midnight) return res4.Value.Date == ld;

                    if (DateTime.TryParse(expectedValue, out var d) && d == d.Date)
                    {
                        return LocalDate.FromDateTime(d) == ld;
                    }

                    return false;
                case LocalDateTime ldt:
                    var res2 = LocalDateTimePattern.ExtendedIso.Parse(expectedValue);
                    if (!res2.Success) return false;
                    return res2.Value == ldt;
                case Instant i:
                    var res3 = InstantPattern.ExtendedIso.Parse(expectedValue);
                    if (!res3.Success) return false;
                    return res3.Value == i;
                case LocalTime t:
                    var res5 = LocalTimePattern.ExtendedIso.Parse(expectedValue);
                    if (!res5.Success) return false;
                    return res5.Value == t;
                case OffsetDateTime odt:
                    var res6 = OffsetDateTimePattern.ExtendedIso.Parse(expectedValue);
                    if (!res6.Success) return false;
                    return res6.Value == odt;
            }

            return false;
        }

        public object Retrieve(KeyValuePair<string, string> keyValuePair, Type targetType, Type propertyType)
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


                if (DateTime.TryParse(keyValuePair.Value, out var d) && d == d.Date)
                {
                    return LocalDate.FromDateTime(d);
                }

                throw GetInvalidOperationException(keyValuePair.Value);
            }

            if (t == typeof(LocalDateTime))
            {
                var res = LocalDateTimePattern.ExtendedIso.Parse(keyValuePair.Value);
                if (res.Success) return res.Value;

                if (DateTime.TryParse(keyValuePair.Value, out var d))
                {
                    return LocalDateTime.FromDateTime(d);
                }

                throw GetInvalidOperationException(keyValuePair.Value);
            }

            if (t == typeof(Instant))
            {
                var res = InstantPattern.ExtendedIso.Parse(keyValuePair.Value);
                if (!res.Success) throw GetInvalidOperationException(keyValuePair.Value);
                return res.Value;
            }

            if (t == typeof(LocalTime))
            {
                var res = LocalTimePattern.ExtendedIso.Parse(keyValuePair.Value);
                if (!res.Success) throw GetInvalidOperationException(keyValuePair.Value);
                return res.Value;
            }

            if (t == typeof(OffsetDateTime))
            {
                var res = OffsetDateTimePattern.ExtendedIso.Parse(keyValuePair.Value);
                if (!res.Success) throw GetInvalidOperationException(keyValuePair.Value);
                return res.Value;
            }

            throw new NotImplementedException();
        }

        private static InvalidOperationException GetInvalidOperationException(string value) => new InvalidOperationException($"Cannot parse value {value} as iso pattern");
    }
}
