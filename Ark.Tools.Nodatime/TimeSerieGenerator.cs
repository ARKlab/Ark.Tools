﻿using System;
using System.Collections.Generic;
using NodaTime;
using Ark.Tools.Nodatime.Intervals;
using EnsureThat;

namespace Ark.Tools.Nodatime
{
    public abstract partial class TimeSerieGenerator
    {
        #region HourlyLocal
        public static IEnumerable<LocalDateTime> HourlyLocal(LocalDate start, LocalDate end)
        {
            Ensure.Bool.IsTrue(start < end);

            return HourlyLocal(new LocalDateRange(start, end));
        }

        public static IEnumerable<LocalDateTime> HourlyLocal(LocalDateTime start, LocalDateTime end)
        {
            Ensure.Bool.IsTrue(start < end);

            return HourlyLocal(new LocalDateTimeRange(start, end));
        }

        public static IEnumerable<LocalDateTime> HourlyLocal(LocalDateRange range)
        {
            return HourlyLocal(range.ToLocalDateTimeRange());
        }

        public static IEnumerable<LocalDateTime> HourlyLocal(LocalDateTimeRange range)
        {

            LocalDateTime c = range.Start;
            while (c < range.End)
            {
                yield return c;
                c = c.PlusHours(1);
            }
        }
        #endregion

        #region HourlyUtc

        public static IEnumerable<Instant> HourlyUtc(LocalDate start, LocalDate end, string timezone)
        {
            Ensure.Bool.IsTrue(start <= end);

            return HourlyUtc((new LocalDateRange(start, end)).InZone(timezone));
        }

        public static IEnumerable<Instant> HourlyUtc(LocalDateTime start, LocalDateTime end, string timezone)
        {
            Ensure.Bool.IsTrue(start <= end);

            return HourlyUtc((new LocalDateTimeRange(start, end)).InZone(timezone));
        }

        public static IEnumerable<Instant> HourlyUtc(LocalDateRange range, string timezone)
        {
            return HourlyUtc(range.InZone(timezone));
        }

        public static IEnumerable<Instant> HourlyUtc(LocalDateTimeRange range, string timezone)
        {
            return HourlyUtc(range.InZone(timezone));
        }

        public static IEnumerable<Instant> HourlyUtc(ZonedDateTimeRange range)
        {
            Instant c = range.Start.ToInstant();
            Instant e = range.End.ToInstant();
            while (c < e)
            {
                yield return c;
                c = c.Plus(Duration.FromHours(1));
            }
        }

        #endregion

        #region Hourly Zoned
        public static IEnumerable<ZonedDateTime> Hourly(ZonedDateTimeRange range)
        {
            return FromDuration(range, Duration.FromHours(1));
        }
        #endregion

        #region Duration
        public static IEnumerable<ZonedDateTime> FromDuration(ZonedDateTimeRange range, Duration duration)
        {
            var c = range.Start;
            var e = range.End;
            while (ZonedDateTime.Comparer.Instant.Compare(c, e) < 0)
            {
                yield return c;
                c = c.Plus(duration);
            }
        }

        public static IEnumerable<ZonedDateTime> FromDuration(ZonedDateTime start, ZonedDateTime end, Duration duration)
        {
            Ensure.Bool.IsTrue(start.ToInstant() < end.ToInstant());

            var range = new ZonedDateTimeRange(start, end);
            return FromDuration(range, duration);
        }
        #endregion

        #region Period
        public static IEnumerable<LocalDate> FromPeriod(LocalDateRange range, Period period)
        {
            var c = range.Start;
            var e = range.End;
            while (c < e)
            {
                yield return c;
                c = c.Plus(period);
            }
        }

        public static IEnumerable<LocalDate> FromPeriod(LocalDate start, LocalDate end, Period period)
        {
            Ensure.Bool.IsTrue(start < end);

            var range = new LocalDateRange(start, end);
            return FromPeriod(range, period);
        }
        #endregion

        #region Daily
        public static IEnumerable<LocalDate> Daily(LocalDate start, LocalDate end)
        {
            Ensure.Bool.IsTrue(start < end);

            return Daily(new LocalDateRange(start, end));
        }

        public static IEnumerable<LocalDate> Daily(LocalDateRange range)
        {
            LocalDate c = range.Start;
            LocalDate e = range.End;
            while (c < e)
            {
                yield return c;
                c = c.PlusDays(1);
            }
        }

        #endregion Daily

        #region Weekly
        public static IEnumerable<LocalDate> Weekly(LocalDate start, LocalDate end)
        {
            Ensure.Bool.IsTrue(start < end);
            Ensure.Bool.IsTrue(start.DayOfWeek == end.DayOfWeek);


            return Weekly(new LocalDateRange(start, end));
        }

        public static IEnumerable<LocalDate> Weekly(LocalDateRange range)
        {
            Ensure.Bool.IsTrue(range.Start.DayOfWeek == range.End.DayOfWeek);

            LocalDate c = range.Start;
            LocalDate e = range.End;
            while (c < e)
            {
                yield return c;
                c = c.PlusWeeks(1);
            }
        }

        #endregion

        #region Monthly
        public static IEnumerable<LocalDate> Monthly(LocalDate start, LocalDate end)
        {
            Ensure.Bool.IsTrue(start < end);
            Ensure.Bool.IsTrue(start.Day == 1);
            Ensure.Bool.IsTrue(end.Day == 1);

            // we generate a next till we reach end (included)
            return Monthly(new LocalDateRange(start, end));
        }

        public static IEnumerable<LocalDate> Monthly(LocalDateRange range)
        {
            Ensure.Bool.IsTrue(range.Start.Day == 1);
            Ensure.Bool.IsTrue(range.End.Day == 1);

            LocalDate c = range.Start;
            LocalDate e = range.End;
            while (c < e)
            {
                yield return c;
                c = c.PlusMonths(1);
            }
        }

        #endregion Monthly

        #region Bimestral
        public static IEnumerable<LocalDate> Bimestraly(LocalDate start, LocalDate end)
        {
            Ensure.Bool.IsTrue(start < end);
            Ensure.Bool.IsTrue(start.Day == 1);
            Ensure.Bool.IsTrue(end.Day == 1);
            Ensure.Bool.IsTrue(((start.Month - 1) % 2) == 0);
            Ensure.Bool.IsTrue(((end.Month - 1) % 2) == 0);

            return Bimestraly(new LocalDateRange(start, end));
        }

        public static IEnumerable<LocalDate> Bimestraly(LocalDateRange range)
        {
            Ensure.Bool.IsTrue(range.Start.Day == 1);
            Ensure.Bool.IsTrue(range.End.Day == 1);
            Ensure.Bool.IsTrue(((range.Start.Month - 1) % 2) == 0);
            Ensure.Bool.IsTrue(((range.End.Month - 1) % 2) == 0);

            LocalDate c = range.Start;
            LocalDate e = range.End;
            while (c < e)
            {
                yield return c;
                c = c.PlusMonths(2);
            }
        }
        #endregion Bimestral

        #region Trimestral
        public static IEnumerable<LocalDate> Trimestraly(LocalDate start, LocalDate end)
        {
            Ensure.Bool.IsTrue(start < end);
            Ensure.Bool.IsTrue(start.Day == 1);
            Ensure.Bool.IsTrue(end.Day == 1);
            Ensure.Bool.IsTrue(((start.Month - 1) % 3) == 0);
            Ensure.Bool.IsTrue(((end.Month - 1) % 3) == 0);

            return Trimestraly(new LocalDateRange(start, end));
        }

        public static IEnumerable<LocalDate> Trimestraly(LocalDateRange range)
        {
            Ensure.Bool.IsTrue(range.Start.Day == 1);
            Ensure.Bool.IsTrue(range.End.Day == 1);
            Ensure.Bool.IsTrue(((range.Start.Month-1) % 3) == 0);
            Ensure.Bool.IsTrue(((range.End.Month-1) % 3) == 0);

            LocalDate c = range.Start;
            LocalDate e = range.End;
            while (c < e)
            {
                yield return c;
                c = c.PlusMonths(3);
            }
        }
        #endregion Trimestral

        #region Yearly
        public static IEnumerable<LocalDate> Yearly(LocalDate start, LocalDate end)
        {
            Ensure.Bool.IsTrue(start < end);
            Ensure.Bool.IsTrue(start.Day == 1);
            Ensure.Bool.IsTrue(end.Day == 1);
            Ensure.Bool.IsTrue(start.Month == 1);
            Ensure.Bool.IsTrue(end.Month == 1);

            return Yearly(new LocalDateRange(start, end));
        }

        public static IEnumerable<LocalDate> Yearly(LocalDateRange range)
        {
            Ensure.Bool.IsTrue(range.Start.Day == 1);
            Ensure.Bool.IsTrue(range.End.Day == 1);
            Ensure.Bool.IsTrue(range.Start.Month == 1);
            Ensure.Bool.IsTrue(range.End.Month == 1);

            LocalDate c = range.Start;
            LocalDate e = range.End;
            while (c < e)
            {
                yield return c;
                c = c.PlusYears(1);
            }
        }
        #endregion Yearly
    }
}
