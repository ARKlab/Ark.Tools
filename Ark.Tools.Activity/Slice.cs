using Ark.Tools.Nodatime;
using NodaTime;
using System;


namespace Ark.Tools.Activity
{
    public struct Slice : IEquatable<Slice>
    {
        internal Slice(ZonedDateTime start)
        {
            SliceStart = start;
        }

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

        public override bool Equals(object obj)
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
