// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;

using NodaTime;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Ark.Tools.Nodatime.Intervals
{
    [StructLayout(LayoutKind.Auto)]
    public readonly struct DateInterval
        : IComparable<DateInterval>
        , IEquatable<DateInterval>
    {
        private readonly DatePeriod _period;
        private readonly LocalDate _start;

        public DateInterval(LocalDate point, DatePeriod period)
        {
            Ensure.Comparable.Is(point, StartOfInterval(point, period), nameof(point));

            _period = period;
            _start = point;
        }

        public static DateInterval IntervalOf(LocalDate time, DatePeriod period)
        {
            return new DateInterval(StartOfInterval(time, period), period);
        }

        public static bool IsStartOfInterval(LocalDateTime time, DatePeriod period)
        {
            return LocalTime.Midnight == time.TimeOfDay && IsStartOfInterval(time.Date, period);
        }

        public static bool IsStartOfInterval(LocalDate date, DatePeriod period)
        {
            return DateInterval.StartOfInterval(date, period) == date;
        }

        public static LocalDate StartOfInterval(LocalDate date, DatePeriod period)
        {
            switch (period)
            {
                case DatePeriod.Day:
                    return date;
                case DatePeriod.Week:
                    return date.FirstDayOfTheWeek();
                case DatePeriod.Month:
                    return date.FirstDayOfTheMonth();
                case DatePeriod.Bimestral:
                    return new LocalDate(date.Year, ((date.Month - 1) / 2) * 2 + 1, 1, date.Calendar);
                case DatePeriod.Trimestral:
                    return date.FirstDayOfTheQuarter();
                case DatePeriod.Calendar:
                    return date.FirstDayOfTheYear();
            }

            return date;
        }

        public static LocalDate EndOfInterval(LocalDate date, DatePeriod period)
        {
            return StartOfInterval(date, period) + GetIncrement(period);
        }

        public readonly LocalDate Date { get { return _start; } }
        public readonly LocalDateRange Range { get { return AsRange(); } }
        public readonly DatePeriod Period { get { return _period; } }

        public readonly DateInterval NextInterval()
        {
            return new DateInterval(_start + GetIncrement(), _period);
        }

        public readonly DateInterval PreviousInterval()
        {
            return new DateInterval(_start - GetIncrement(), _period);
        }

        public readonly DateInterval NextInterval(uint count)
        {
            return new DateInterval(_start + GetIncrement(count), _period);
        }

        public readonly DateInterval PreviousInterval(uint count)
        {
            return new DateInterval(_start - GetIncrement(count), _period);
        }

        public readonly bool CanSplitInto(DatePeriod period)
        {
            return _isValidperiodSplit(_period, period);
        }

        public readonly DateInterval LastOf(DatePeriod period)
        {
            Ensure.Bool.IsTrue(CanSplitInto(period));

            var next = NextInterval();
            var changeperiod = new DateInterval(next._start, period);
            return changeperiod.PreviousInterval();
        }

        public readonly IEnumerable<TimeInterval> SplitInto(TimePeriod period, string timezone)
        {
            return SplitInto(period, DateTimeZoneProviders.Tzdb[timezone]);
        }

        public readonly IEnumerable<TimeInterval> SplitInto(TimePeriod period, DateTimeZone timezone)
        {
            EnsureArg.IsNotNull(timezone);

            var s = timezone.AtStartOfDay(_start);
            var n = timezone.AtStartOfDay(_start + GetIncrement());

            var c = new TimeInterval(s, period);
            var e = new TimeInterval(n, period);

            while (c < e)
            {
                yield return c;
                c = c.NextInterval();
            }
        }

        public readonly IEnumerable<DateInterval> SplitInto(DatePeriod period)
        {
            Ensure.Bool.IsTrue(CanSplitInto(period));


            var s = _start;
            var n = NextInterval()._start;

            var c = new DateInterval(s, period);
            var e = new DateInterval(n, period);

            while (c < e)
            {
                yield return c;
                c = c.NextInterval();
            }
        }

        public readonly LocalDateRange AsRange()
        {
            return new LocalDateRange(_start, _start + GetIncrement());
        }

        public readonly bool Contains(DateInterval period)
        {
            return this.Range.Contains(period.Range);
        }

        public readonly Period GetIncrement()
        {
            return DateInterval.GetIncrement(_period);
        }

        public readonly Period GetIncrement(uint count)
        {
            return DateInterval.GetIncrement(_period, count);
        }

        public static Period GetIncrement(DatePeriod period)
        {
            return DateInterval.GetIncrement(period, 1);
        }

        public static Period GetIncrement(DatePeriod period, uint count)
        {

            var c = (int)count;
            switch (period)
            {
                case DatePeriod.Day:
                    return NodaTime.Period.FromDays(1 * c);
                case DatePeriod.Week:
                    return NodaTime.Period.FromWeeks(1 * c);
                case DatePeriod.Month:
                    return NodaTime.Period.FromMonths(1 * c);
                case DatePeriod.Bimestral:
                    return NodaTime.Period.FromMonths(2 * c);
                case DatePeriod.Trimestral:
                    return NodaTime.Period.FromMonths(3 * c);
                case DatePeriod.Calendar:
                    return NodaTime.Period.FromYears(1 * c);
            }

            throw new ArgumentOutOfRangeException(nameof(period));
        }

        private static bool _isValidperiodSplit(DatePeriod source, DatePeriod target)
        {
            return target < source && target != DatePeriod.Week;
        }

        public readonly int CompareTo(DateInterval other)
        {
            Ensure.Bool.IsTrue(Period == other.Period);
            return _start.CompareTo(other._start);
        }

        private static int CompareTo(DateInterval x, DateInterval y)
        {
            return x.CompareTo(y);
        }

        public override readonly int GetHashCode()
        {
            unchecked
            {
                int hash = 7243;
                hash = hash * 92821 + _start.GetHashCode();
                hash = hash * 92821 + _period.GetHashCode();
                return hash;
            }
        }

        public readonly bool Equals(DateInterval other)
        {
            return _start == other._start && _period == other._period;
        }

        public static bool operator ==(DateInterval x, DateInterval y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(DateInterval x, DateInterval y)
        {
            return !(x == y);
        }

        public static bool operator <(DateInterval x, DateInterval y)
        {
            return CompareTo(x, y) < 0;
        }

        public static bool operator >(DateInterval x, DateInterval y)
        {

            return CompareTo(x, y) > 0;

        }

        public static bool operator <=(DateInterval x, DateInterval y)
        {

            return CompareTo(x, y) <= 0;

        }

        public static bool operator >=(DateInterval x, DateInterval y)
        {
            return CompareTo(x, y) >= 0;
        }

        public override readonly bool Equals(object? obj)
        {
            if (obj is not DateInterval)
                return false;

            return Equals((DateInterval)obj);
        }
    }
}