﻿using Ark.Tools.Nodatime;
using EnsureThat;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using NodaTime.Text;
using System;


namespace Ark.Tools.Activity
{
    internal sealed class ZonedDateTimeTzdbConverter : JsonConverter
    {
        private JsonConverter _converter = new NodaPatternConverter<ZonedDateTime>(
                ZonedDateTimePattern.CreateWithInvariantCulture("uuuu'-'MM'-'dd'T'HH':'mm':'ss;FFFFFFFFFo<G> z", DateTimeZoneProviders.Tzdb)
            , x => Ensure.Bool.IsTrue(x.Calendar == CalendarSystem.Iso)
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

        public Slice MoveDays(int days)
        {
            return Slice.From(SliceStart.LocalDateTime.PlusDays(days).InZoneLeniently(SliceStart.Zone));
        }

        public Slice MoveAtStartOfWeek(IsoDayOfWeek dayOfWeek = IsoDayOfWeek.Monday)
        {
            return Slice.From(SliceStart.LocalDateTime.Date.FirstDayOfTheWeek(dayOfWeek).AtMidnight().InZoneLeniently(SliceStart.Zone));
        }

        public Slice MoveAtStartOfMonth()
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

        public bool Equals(Slice other)
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

        public override bool Equals(object? obj)
        {
            if (!(obj is Slice))
                return false;

            return Equals((Slice)obj);
        }

        public override int GetHashCode()
        {
            return SliceStart.GetHashCode();
        }

        public override string ToString()
        {
            return SliceStart.ToString("F", null);
        }
    }
}
