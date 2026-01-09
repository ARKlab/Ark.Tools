// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

using Ark.Tools.Core;

using NodaTime;

using System.Runtime.InteropServices;

namespace Ark.Tools.Nodatime.Intervals;

[StructLayout(LayoutKind.Auto)]
public readonly struct TimeInterval
    : IComparable<TimeInterval>
    , IEquatable<TimeInterval>
{
    private readonly TimePeriod _period;
    private readonly ZonedDateTime _start;

    public TimeInterval(ZonedDateTime point, TimePeriod period)
    {
        var expectedStart = TimeInterval.StartOfInterval(point, period);
        ArgumentException.ThrowUnless(
            point.ToInstant() == expectedStart.ToInstant(),
            $"Point must be the start of the interval for the given period. Expected: {expectedStart}, Actual: {point}",
            nameof(point));

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
        offset += Duration.FromSeconds(time.Second);

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


    public readonly ZonedDateTime Time { get { return _start; } }
    public readonly ZonedDateTimeRange Range
    { get { return AsRange(); } }
    public readonly TimePeriod Period { get { return _period; } }

    public readonly TimeInterval NextInterval()
    {
        return new TimeInterval(_start + GetIncrement(), _period);
    }

    public readonly TimeInterval PreviousInterval()
    {
        return new TimeInterval(_start - GetIncrement(), _period);
    }

    public readonly TimeInterval NextInterval(uint count)
    {
        return new TimeInterval(_start + GetIncrement(count), _period);
    }

    public readonly TimeInterval PreviousInterval(uint count)
    {
        return new TimeInterval(_start - GetIncrement(count), _period);
    }

    public readonly bool CanSplitInto(TimePeriod period)
    {
        return _isValidperiodSplit(_period, period);
    }

    public readonly TimeInterval LastOf(TimePeriod period)
    {
        InvalidOperationException.ThrowUnless(CanSplitInto(period));

        var next = NextInterval();
        var changeperiod = new TimeInterval(next._start, period);
        return changeperiod.PreviousInterval();
    }

    public readonly IEnumerable<TimeInterval> SplitInto(TimePeriod period)
    {
        InvalidOperationException.ThrowUnless(CanSplitInto(period));

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

    public readonly ZonedDateTimeRange AsRange()
    {
        return new ZonedDateTimeRange(_start, _start + GetIncrement());
    }

    public readonly bool Contains(TimeInterval period)
    {
        return this.Range.Contains(period.Range);
    }

    public readonly Duration GetIncrement()
    {
        return TimeInterval.GetIncrement(_period);
    }
    public readonly Duration GetIncrement(uint count)
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
                return Duration.FromHours(1 * count);
            case TimePeriod.Minute:
                return Duration.FromMinutes(1 * count);
            case TimePeriod.TenMinutes:
                return Duration.FromMinutes(10 * count);
            case TimePeriod.QuarterHour:
                return Duration.FromMinutes(15 * count);
            case TimePeriod.HalfHour:
                return Duration.FromMinutes(30 * count);
        }

        throw new ArgumentOutOfRangeException(nameof(period));
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

        throw new ArgumentOutOfRangeException(nameof(period));
    }

    private static bool _isValidperiodSplit(TimePeriod source, TimePeriod target)
    {
        return target < source;
    }

    public readonly int CompareTo(TimeInterval other)
    {
        InvalidOperationException.ThrowUnless(Period == other.Period);
        return ZonedDateTime.Comparer.Instant.Compare(_start, other._start);
    }

    private static int CompareTo(TimeInterval x, TimeInterval y)
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

    public readonly bool Equals(TimeInterval other)
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

    public override readonly bool Equals(object? obj)
    {
        if (obj is not TimeInterval)
            return false;

        return Equals((TimeInterval)obj);
    }
}