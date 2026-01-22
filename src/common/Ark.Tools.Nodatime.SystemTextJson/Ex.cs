using NodaTime;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.Nodatime.SystemTextJson;

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
            .ConfigureForNodaTimeRanges()
            ;
    }

    private static void _addDefaultConverters(IList<JsonConverter> converters, IDateTimeZoneProvider provider)
    {
        _ensureThis(converters, Converters.InstantConverter.Instance);

        _ensureThis(converters, Converters.LocalDateConverter.Instance);
        _ensureThis(converters, Converters.LocalDateTimeConverter.Instance);
        _ensureThis(converters, Converters.LocalTimeConverter.Instance);

        _ensureThis(converters, Converters.AnnualDateConverter.Instance);
        
        _ensureThis(converters, Converters.TzdbDateTimeZoneConverter.Instance);
        _ensureThis(converters, Converters.TzdbZonedDateTimeConverter.Instance);

        _ensureThis(converters, Converters.RoundtripDurationConverter.Instance);
        _ensureThis(converters, Converters.RoundtripPeriodConverter.Instance);

        _ensureThis(converters, Converters.IsoDateIntervalConverter.Instance);
        _ensureThis(converters, Converters.IsoIntervalConverter.Instance);

        _ensureThis(converters, Converters.OffsetTimeConverter.Instance);
        _ensureThis(converters, Converters.OffsetDateConverter.Instance);
        _ensureThis(converters, Converters.OffsetConverter.Instance);
        _ensureThis(converters, Converters.ExtendedIsoOffsetDateTimeConverter.Instance);
    }

    private static void _ensureThis<T>(IList<JsonConverter> converters, JsonConverter<T> converter)
    {
        for (int i = converters.Count - 1; i >= 0; i--)
        {
            if (converters[i].CanConvert(typeof(T)))
            {
                converters.RemoveAt(i);
            }
        }
        converters.Insert(0, converter);
    }
}