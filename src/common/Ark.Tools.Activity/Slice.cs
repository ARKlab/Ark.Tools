using Ark.Tools.Nodatime;


using Newtonsoft.Json;

using NodaTime;
using NodaTime.Serialization.JsonNet;
using NodaTime.Text;

using System;
using Ark.Tools.Core;


namespace Ark.Tools.Activity
{
    internal sealed class ZonedDateTimeTzdbConverter : JsonConverter
    {
        private readonly JsonConverter _converter = new NodaPatternConverter<ZonedDateTime>(
                ZonedDateTimePattern.CreateWithInvariantCulture("uuuu'-'MM'-'dd'T'HH':'mm':'ss;FFFFFFFFFo<G> z", DateTimeZoneProviders.Tzdb)
            , x => { if (x.Calendar != CalendarSystem.Iso) { throw new InvalidOperationException("Only ISO calendar system is supported"); } }
            );

        public override bool CanRead => _converter.CanRead;
        public override bool CanWrite => _converter.CanWrite;

        public override bool CanConvert(Type objectType)
            => _converter.CanConvert(objectType);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            => _converter.ReadJson(reader, objectType, existingValue, serializer);

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            => _converter.WriteJson(writer, value, serializer);
    }

    public struct Slice : IEquatable<Slice>
    {
        internal Slice(ZonedDateTime start)
        {
            SliceStart = start;
        }

        [JsonConverter(typeof(ZonedDateTimeTzdbConverter))]
        public ZonedDateTime SliceStart;

        public readonly Slice MoveDays(int days)
        {
            return Slice.From(SliceStart.LocalDateTime.PlusDays(days).InZoneLeniently(SliceStart.Zone));
        }

        public readonly Slice MoveAtStartOfWeek(IsoDayOfWeek dayOfWeek = IsoDayOfWeek.Monday)
        {
            return Slice.From(SliceStart.LocalDateTime.Date.FirstDayOfTheWeek(dayOfWeek).AtMidnight().InZoneLeniently(SliceStart.Zone));
        }

        public readonly Slice MoveAtStartOfMonth()
        {
            return Slice.From(SliceStart.LocalDateTime.Date.FirstDayOfTheMonth().AtMidnight().InZoneLeniently(SliceStart.Zone));
        }

        public static Slice From(ZonedDateTime start)
        {
            return new Slice(start);
        }

        public static Slice From(LocalDate start, string timezone)
        {
            return new Slice(start.AtMidnight().InZoneStrictly(DateTimeZoneProviders.Tzdb[timezone]));
        }

        public readonly bool Equals(Slice other)
        {
            return SliceStart == other.SliceStart;
        }

        public static bool operator ==(Slice x, Slice y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Slice x, Slice y)
        {
            return !x.Equals(y);
        }

        public override readonly bool Equals(object? obj)
        {
            if (!(obj is Slice))
                return false;

            return Equals((Slice)obj);
        }

        public override readonly int GetHashCode()
        {
            return SliceStart.GetHashCode();
        }

        public override readonly string ToString()
        {
            return SliceStart.ToString("F", null);
        }
    }
}