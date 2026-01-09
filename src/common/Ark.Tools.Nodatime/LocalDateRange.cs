// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;


using System.Globalization;
using System.Runtime.InteropServices;

namespace Ark.Tools.Nodatime;

[StructLayout(LayoutKind.Auto)]
public struct LocalDateRange
    : IEquatable<LocalDateRange>
{
    private LocalDate _start, _end;

    public LocalDateRange(LocalDate start, LocalDate end)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(start, end, nameof(start));

        _start = start;
        _end = end;
    }
    public override readonly string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "Start:{0} | End:{1}", _start, _end);
    }


    public LocalDate Start
    {
        readonly get { return _start; }
        set
        {
            _start = value;
        }
    }
    public LocalDate End
    {
        readonly get { return _end; }
        set
        {
            _end = value;
        }
    }

    public readonly bool Contains(LocalDate ld)
    {
        return ld >= _start && ld < _end;
    }

    public readonly bool Contains(LocalDateTime ldt)
    {
        return ldt.Date >= _start && ldt.Date < End;
    }

    public readonly bool Contains(LocalDateRange other)
    {
        return other._start >= _start && other._end <= _end;
    }

    public readonly bool Overlaps(LocalDateRange other)
    {
        return _start < other._end && _end > other._start;
    }

    public readonly bool OverlapsOrContiguous(LocalDateRange other)
    {
        return Overlaps(other) || IsContiguous(other);
    }

    public readonly bool IsContiguous(LocalDateRange other)
    {
        return _start == other._end || _end == other._start;
    }

    public readonly LocalDateRange MergeOverlapsOrContiguous(LocalDateRange other)
    {
        InvalidOperationException.ThrowUnless(OverlapsOrContiguous(other), "Ranges must overlap or be contiguous to merge.");
        return new LocalDateRange(_start.MinWith(other._start), _end.MaxWith(other._end));
    }

    public readonly LocalDateRange Merge(LocalDateRange other)
    {
        return new LocalDateRange(_start.MinWith(other._start), _end.MaxWith(other._end));
    }

    public readonly IEnumerable<LocalDateRange> Subtract(LocalDateRange other)
    {
        //      |------------|
        //  |--------------------|
        if (other.Contains(this))
            return Array.Empty<LocalDateRange>();
        //  |----------------|
        //                      |----|
        else if (!Overlaps(other))
            return [this];
        else if (Contains(other))
        {
            //  |----------------|
            //  |----|
            if (_start == other._start)
                return [new LocalDateRange(other._end, _end)];
            //  |----------------|
            //              |----|
            else if (_end == other._end)
                return [new LocalDateRange(_start, other._start)];
            //  |----------------|
            //        |----|
            else
                return [
                    new LocalDateRange(_start, other._start),
                    new LocalDateRange(other._end, _end)
                ];
        }
        else
        {
            //      |----------------|
            //   |----|
            if (other.Contains(_start))
                return [new LocalDateRange(other._end, _end)];
            //  |----------------|
            //                |----|
            else
                return [new LocalDateRange(_start, other._start)];
        }
    }

    public readonly LocalDateTimeRange ToLocalDateTimeRange()
    {
        return new LocalDateTimeRange(_start.AtMidnight(), _end.AtMidnight());
    }

    public readonly ZonedDateTimeRange ToZonedDateTimeRange(string timezone)
    {
        return ToLocalDateTimeRange().ToZonedDateTimeRange(timezone);
    }

    public readonly ZonedDateTimeRange ToZonedDateTimeRange(DateTimeZone dateTimeZone)
    {
        return ToLocalDateTimeRange().ToZonedDateTimeRange(dateTimeZone);
    }

    public readonly ZonedDateTimeRange InZone(string timezone)
    {
        return ToZonedDateTimeRange(timezone);
    }

    public readonly ZonedDateTimeRange InZone(DateTimeZone dateTimeZone)
    {
        return ToZonedDateTimeRange(dateTimeZone);
    }

    public readonly ZonedDateTimeRange InUtc()
    {
        return ToZonedDateTimeRange(DateTimeZone.Utc);
    }


    public override readonly int GetHashCode()
    {
        unchecked
        {
            int hash = 7243;
            hash = hash * 92821 + Start.GetHashCode();
            hash = hash * 92821 + End.GetHashCode();
            return hash;
        }
    }

    public readonly bool Equals(LocalDateRange other)
    {
        return Start == other._start && End == other._end;
    }

    public static bool operator ==(LocalDateRange x, LocalDateRange y)
    {
        return x.Equals(y);
    }

    public static bool operator !=(LocalDateRange x, LocalDateRange y)
    {
        return !(x == y);
    }

    public override readonly bool Equals(object? obj)
    {
        if (obj is not LocalDateRange)
            return false;

        return Equals((LocalDateRange)obj);
    }
}