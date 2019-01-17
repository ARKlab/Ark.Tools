﻿// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using EnsureThat;
using NodaTime;
using System;
using System.Collections.Generic;

namespace Ark.Tools.Nodatime.Intervals
{
    public struct TimeInterval
        : IComparable<TimeInterval>
        , IEquatable<TimeInterval>
    {
        private readonly TimePeriod _period;
        private readonly ZonedDateTime _start;

        public TimeInterval(ZonedDateTime point, TimePeriod period)
        {
            Ensure.Comparable.Is(point.ToInstant(), TimeInterval.StartOfInterval(point, period).ToInstant(), nameof(point));

            _period = period;
            _start = point;
        }

        public static TimeInterval IntervalOf(ZonedDateTime time, TimePeriod period)
        {
            return new TimeInterval(TimeInterval.StartOfInterval(time, period), period);
        }

        public static TimeInterval IntervalOf(LocalDateTime time, TimePeriod period)
        {
            return new TimeInterval(StartOfInterval(time.InUtc(), period), period);
        }

        public static ZonedDateTime StartOfInterval(ZonedDateTime time, TimePeriod period)
        {
            return time - _offsetFromStart(time.TimeOfDay, period);
        }

        public static LocalDateTime StartOfInterval(LocalDateTime time, TimePeriod period)
        {
            return (time.InUtc() - _offsetFromStart(time.TimeOfDay, period)).LocalDateTime;
        }

        public static bool IsStartOfInterval(LocalDateTime time, TimePeriod period)
        {
            return TimeInterval.StartOfInterval(time, period) == time;
        }

        public static bool IsStartOfInterval(ZonedDateTime time, TimePeriod period)
        {
            return TimeInterval.StartOfInterval(time, period) == time;
        }

        public static LocalDateTime EndOfInterval(LocalDateTime time, TimePeriod period)
        {
            return StartOfInterval(time, period) + _getIncrementPeriod(period);
        }

        public static ZonedDateTime EndOfInterval(ZonedDateTime time, TimePeriod period)
        {
            return StartOfInterval(time, period) + GetIncrement(period);
        }

        private static Duration _offsetFromStart(LocalTime time, TimePeriod period)
        {
            var offset = Duration.Zero;
            offset += Duration.FromTicks(time.TickOfSecond);
            offset += Duration.FromMilliseconds(time.Second);

            switch (period)
            {
                case TimePeriod.Hour:
                    offset += Duration.FromMinutes(time.Minute);
                    break;
                case TimePeriod.Minute:
                    break;
                case TimePeriod.TenMinutes:
                    offset += Duration.FromMinutes(time.Minute % 10);
                    break;
                case TimePeriod.QuarterHour:
                    offset += Duration.FromMinutes(time.Minute % 15);
                    break;
                case TimePeriod.HalfHour:
                    offset += Duration.FromMinutes(time.Minute % 30);
                    break;
            }

            return offset;
        }


        public ZonedDateTime Time { get { return _start; } }
        public ZonedDateTimeRange Range { get { return AsRange(); } }
        public TimePeriod Period { get { return _period; } }

        public TimeInterval NextInterval()
        {
            return new TimeInterval(_start + GetIncrement(), _period);
        }

        public TimeInterval PreviousInterval()
        {
            return new TimeInterval(_start - GetIncrement(), _period);
        }

        public TimeInterval NextInterval(uint count)
        {
            return new TimeInterval(_start + GetIncrement(count), _period);
        }

        public TimeInterval PreviousInterval(uint count)
        {
            return new TimeInterval(_start - GetIncrement(count), _period);
        }

        public bool CanSplitInto(TimePeriod period)
        {
            return _isValidperiodSplit(_period, period);
        }

        public TimeInterval LastOf(TimePeriod period)
        {
            Ensure.Bool.IsTrue(CanSplitInto(period));

            var next = NextInterval();
            var changeperiod = new TimeInterval(next._start, period);
            return changeperiod.PreviousInterval();
        }

        public IEnumerable<TimeInterval> SplitInto(TimePeriod period)
        {
            Ensure.Bool.IsTrue(CanSplitInto(period));

            var s = _start;
            var n = NextInterval()._start;

            var c = new TimeInterval(s, period);
            var e = new TimeInterval(n, period);

            while (c < e)
            {
                yield return c;
                c = c.NextInterval();
            }
        }

        public ZonedDateTimeRange AsRange()
        {
            return new ZonedDateTimeRange(_start, _start + GetIncrement());
        }

        public bool Contains(TimeInterval period)
        {
            return this.Range.Contains(period.Range);
        }

        public Duration GetIncrement()
        {
            return TimeInterval.GetIncrement(_period);
        }
        public Duration GetIncrement(uint count)
        {
            return TimeInterval.GetIncrement(_period, count);
        }

        public static Duration GetIncrement(TimePeriod period)
        {
            return TimeInterval.GetIncrement(period, 1);
        }

        public static Duration GetIncrement(TimePeriod period, uint count)
        {
            switch (period)
            {
                case TimePeriod.Hour:
                    return Duration.FromHours(1*count);
                case TimePeriod.Minute:
                    return Duration.FromMinutes(1*count);
                case TimePeriod.TenMinutes:
                    return Duration.FromMinutes(10*count);
                case TimePeriod.QuarterHour:
                    return Duration.FromMinutes(15 * count);
                case TimePeriod.HalfHour:
                    return Duration.FromMinutes(30 * count);
            }

            throw new ArgumentOutOfRangeException("period");
        }

        private static Period _getIncrementPeriod(TimePeriod period)
        {
            switch (period)
            {
                case TimePeriod.Hour:
                    return NodaTime.Period.FromHours(1);
                case TimePeriod.Minute:
                    return NodaTime.Period.FromMinutes(1);
                case TimePeriod.TenMinutes:
                    return NodaTime.Period.FromMinutes(10);
                case TimePeriod.QuarterHour:
                    return NodaTime.Period.FromMinutes(15);
                case TimePeriod.HalfHour:
                    return NodaTime.Period.FromMinutes(30);
            }

            throw new ArgumentOutOfRangeException("period");
        }

        private static bool _isValidperiodSplit(TimePeriod source, TimePeriod target)
        {
            return target < source;
        }

        public int CompareTo(TimeInterval other)
        {
            Ensure.Bool.IsTrue(Period == other.Period);
            return ZonedDateTime.Comparer.Instant.Compare(_start, other._start);
        }

        private static int CompareTo(TimeInterval x, TimeInterval y)
        {
            return x.CompareTo(y);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 7243;
                hash = hash * 92821 + _start.GetHashCode();
                hash = hash * 92821 + _period.GetHashCode();
                return hash;
            }
        }

        public bool Equals(TimeInterval other)
        {
            return _start == other._start && _period == other._period;
        }

        public static bool operator ==(TimeInterval x, TimeInterval y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(TimeInterval x, TimeInterval y)
        {
            return !(x == y);
        }

        public static bool operator <(TimeInterval x, TimeInterval y)
        {
            return CompareTo(x, y) < 0;
        }

        public static bool operator >(TimeInterval x, TimeInterval y)
        {
            return CompareTo(x, y) > 0;
        }

        public static bool operator <=(TimeInterval x, TimeInterval y)
        {
            return CompareTo(x, y) <= 0;
        }

        public static bool operator >=(TimeInterval x, TimeInterval y)
        {
            return CompareTo(x, y) >= 0;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is TimeInterval))
                return false;

            return Equals((TimeInterval)obj);
        }
    }
}
