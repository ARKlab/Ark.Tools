using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using NodaTime.Text;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.Nodatime.SystemTextJson
{
    public static class Ex
    {
        public static JsonSerializerOptions ConfigureForNodaTimeRanges(this JsonSerializerOptions @this)
        {
            if (!@this.Converters.OfType<LocalDateRangeConverter>().Any())
                @this.Converters.Add(new LocalDateRangeConverter());

            if (!@this.Converters.OfType<LocalDateTimeRangeConverter>().Any())
                @this.Converters.Add(new LocalDateTimeRangeConverter());

            if (!@this.Converters.OfType<ZonedDateTimeRangeConverter>().Any())
                @this.Converters.Add(new ZonedDateTimeRangeConverter());

            return @this;
        }

        public static JsonSerializerOptions ConfigureForNodaTimeArkDefaults(this JsonSerializerOptions @this)
        {
            _addDefaultConverters(@this.Converters, DateTimeZoneProviders.Tzdb);

            return @this
                .WithIsoDateIntervalConverter()
                .WithIsoIntervalConverter()
                .ConfigureForNodaTimeRanges()
                ;
        }

        private static void _addDefaultConverters(IList<JsonConverter> converters, IDateTimeZoneProvider provider)
        {
            converters.Insert(0, NodaConverters.InstantConverter);
            converters.Insert(0, NodaConverters.IntervalConverter);
            converters.Insert(0, NodaConverters.LocalDateConverter);
            converters.Insert(0, NodaConverters.LocalDateTimeConverter);
            converters.Insert(0, NodaConverters.LocalTimeConverter);
            converters.Insert(0, NodaConverters.AnnualDateConverter);
            converters.Insert(0, NodaConverters.DateIntervalConverter);
            converters.Insert(0, NodaConverters.OffsetConverter);
            converters.Insert(0, NodaConverters.CreateDateTimeZoneConverter(provider));
            converters.Insert(0, NodaConverters.DurationConverter);
            converters.Insert(0, NodaConverters.RoundtripPeriodConverter);
            converters.Insert(0, NodaConverters.OffsetDateTimeConverter);
            converters.Insert(0, NodaConverters.OffsetDateConverter);

            //converters.Insert(0, NodaConverters.OffsetTimeConverter); 
            converters.Add(new NodaPatternConverter<OffsetDateTime>(
                OffsetDateTimePattern.ExtendedIso, x =>
                {
                    if (x.Calendar != CalendarSystem.Iso) throw new ArgumentException("Calendar must be Iso when serializing", typeof(OffsetDateTime).Name);
                }));

            converters.Insert(0, NodaConverters.CreateZonedDateTimeConverter(provider));
        }
    }
}