// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

using Ark.Tools.Core;

using NodaTime;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Nodatime(net10.0)', Before:
namespace Ark.Tools.Nodatime
{
    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public readonly struct ZonedDateTimeRange
        : IEquatable<ZonedDateTimeRange>
    {
        private readonly ZonedDateTime _start, _end;

        public ZonedDateTimeRange(ZonedDateTime start, ZonedDateTime end)
        {
            InvalidOperationException.ThrowUnless(ZonedDateTime.Comparer.Instant.Compare(start, end) < 0);
            InvalidOperationException.ThrowUnless(start.Zone.Equals(end.Zone));

            _start = start;
            _end = end;
        }
        public override readonly string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "Start:{0} | End:{1}", _start, _end);
        }


        public readonly ZonedDateTime Start { get { return _start; } }
        public readonly ZonedDateTime End { get { return _end; } }
        public readonly DateTimeZone Zone { get { return _start.Zone; } }

        public readonly bool Contains(ZonedDateTime ldt)
        {
            InvalidOperationException.ThrowUnless(ldt.Zone.Equals(Start.Zone));
            return ldt.ToInstant() >= _start.ToInstant() && ldt.ToInstant() < _end.ToInstant();
        }

        public readonly bool Contains(ZonedDateTimeRange other)
        {
            InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
            return other._start.ToInstant() >= _start.ToInstant() && other._end.ToInstant() <= _end.ToInstant();
        }

        public readonly bool Overlaps(ZonedDateTimeRange other)
        {
            InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
            return _start.ToInstant() < other._end.ToInstant() && _end.ToInstant() > other._start.ToInstant();
        }

        public readonly bool OverlapsOrContiguous(ZonedDateTimeRange other)
        {
            InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
            return Overlaps(other) || IsContiguous(other);
        }

        public readonly bool IsContiguous(ZonedDateTimeRange other)
        {
            InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
            return _start == other._end || _end == other._start;
        }
        public readonly ZonedDateTimeRange MergeOverlapsOrContiguous(ZonedDateTimeRange other)
        {
            InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
            InvalidOperationException.ThrowUnless(OverlapsOrContiguous(other));
            return new ZonedDateTimeRange(
                  _start.ToInstant() < other._start.ToInstant() ? _start : other._start
                , _end.ToInstant() > other._end.ToInstant() ? _end : other._end
                );
        }

        public readonly ZonedDateTimeRange Merge(ZonedDateTimeRange other)
        {
            InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
            return new ZonedDateTimeRange(
                _start.ToInstant() < other._start.ToInstant() ? _start : other._start,
                _end.ToInstant() > other._end.ToInstant() ? _end : other._end
                );
        }

        public readonly IEnumerable<ZonedDateTimeRange> Subtract(ZonedDateTimeRange other)
        {
            InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));

            //      |------------|
            //  |--------------------|
            if (other.Contains(this))
                return Array.Empty<ZonedDateTimeRange>();
            //  |----------------|
            //                      |----|
            else if (!Overlaps(other))
                return [this];
            else if (Contains(other))
            {
                //  |----------------|
                //  |----|
                if (_start == other._start)
                    return [new ZonedDateTimeRange(other._end, _end)];
                //  |----------------|
                //              |----|
                else if (_end == other._end)
                    return [new ZonedDateTimeRange(_start, other._start)];
                //  |----------------|
                //        |----|
                else
                    return [
                        new ZonedDateTimeRange(_start, other._start),
                        new ZonedDateTimeRange(other._end, _end)
                    ];
            }
            else
            {
                //      |----------------|
                //   |----|
                if (other.Contains(_start))
                    return [new ZonedDateTimeRange(other._end, _end)];
                //  |----------------|
                //                |----|
                else
                    return [new ZonedDateTimeRange(_start, other._start)];
            }
        }

        public readonly LocalDateRange ToLocalDateRangeLeniently()
        {
            return new LocalDateRange(_start.Date, _end.TimeOfDay == LocalTime.Midnight ? _end.Date : _end.Date.PlusDays(1));
        }

        public readonly LocalDateRange ToLocalDateRangeStrict()
        {
            InvalidOperationException.ThrowUnless(Start.TimeOfDay == LocalTime.Midnight);
            InvalidOperationException.ThrowUnless(End.TimeOfDay == LocalTime.Midnight);

            return new LocalDateRange(_start.Date, _end.Date);
        }

        public readonly LocalDateTimeRange ToLocalDateTimeRange()
        {
            return new LocalDateTimeRange(_start.LocalDateTime, _end.LocalDateTime);
        }

        public readonly ZonedDateTimeRange WithZone(string timezone)
        {
            var zone = DateTimeZoneProviders.Tzdb[timezone];
            return WithZone(zone);
        }

        public readonly ZonedDateTimeRange WithZone(DateTimeZone dateTimeZone)
        {
            return new ZonedDateTimeRange(_start.WithZone(dateTimeZone), _end.WithZone(dateTimeZone));
        }

        public readonly ZonedDateTimeRange InUtc()
        {
            return WithZone(DateTimeZone.Utc);
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

        public readonly bool Equals(ZonedDateTimeRange other)
        {
            return _start == other._start && _end == other._end;
        }

        public static bool operator ==(ZonedDateTimeRange x, ZonedDateTimeRange y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(ZonedDateTimeRange x, ZonedDateTimeRange y)
        {
            return !x.Equals(y);
        }

        public override readonly bool Equals(object? obj)
        {
            if (obj is not ZonedDateTimeRange)
                return false;

            return Equals((ZonedDateTimeRange)obj);
        }

        private readonly string GetDebuggerDisplay()
        {
            return ToString();
        }
=======
namespace Ark.Tools.Nodatime;

[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct ZonedDateTimeRange
    : IEquatable<ZonedDateTimeRange>
{
    private readonly ZonedDateTime _start, _end;

    public ZonedDateTimeRange(ZonedDateTime start, ZonedDateTime end)
    {
        InvalidOperationException.ThrowUnless(ZonedDateTime.Comparer.Instant.Compare(start, end) < 0);
        InvalidOperationException.ThrowUnless(start.Zone.Equals(end.Zone));

        _start = start;
        _end = end;
    }
    public override readonly string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "Start:{0} | End:{1}", _start, _end);
    }


    public readonly ZonedDateTime Start { get { return _start; } }
    public readonly ZonedDateTime End { get { return _end; } }
    public readonly DateTimeZone Zone { get { return _start.Zone; } }

    public readonly bool Contains(ZonedDateTime ldt)
    {
        InvalidOperationException.ThrowUnless(ldt.Zone.Equals(Start.Zone));
        return ldt.ToInstant() >= _start.ToInstant() && ldt.ToInstant() < _end.ToInstant();
    }

    public readonly bool Contains(ZonedDateTimeRange other)
    {
        InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
        return other._start.ToInstant() >= _start.ToInstant() && other._end.ToInstant() <= _end.ToInstant();
    }

    public readonly bool Overlaps(ZonedDateTimeRange other)
    {
        InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
        return _start.ToInstant() < other._end.ToInstant() && _end.ToInstant() > other._start.ToInstant();
    }

    public readonly bool OverlapsOrContiguous(ZonedDateTimeRange other)
    {
        InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
        return Overlaps(other) || IsContiguous(other);
    }

    public readonly bool IsContiguous(ZonedDateTimeRange other)
    {
        InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
        return _start == other._end || _end == other._start;
    }
    public readonly ZonedDateTimeRange MergeOverlapsOrContiguous(ZonedDateTimeRange other)
    {
        InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
        InvalidOperationException.ThrowUnless(OverlapsOrContiguous(other));
        return new ZonedDateTimeRange(
              _start.ToInstant() < other._start.ToInstant() ? _start : other._start
            , _end.ToInstant() > other._end.ToInstant() ? _end : other._end
            );
    }

    public readonly ZonedDateTimeRange Merge(ZonedDateTimeRange other)
    {
        InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
        return new ZonedDateTimeRange(
            _start.ToInstant() < other._start.ToInstant() ? _start : other._start,
            _end.ToInstant() > other._end.ToInstant() ? _end : other._end
            );
    }

    public readonly IEnumerable<ZonedDateTimeRange> Subtract(ZonedDateTimeRange other)
    {
        InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));

        //      |------------|
        //  |--------------------|
        if (other.Contains(this))
            return Array.Empty<ZonedDateTimeRange>();
        //  |----------------|
        //                      |----|
        else if (!Overlaps(other))
            return [this];
        else if (Contains(other))
        {
            //  |----------------|
            //  |----|
            if (_start == other._start)
                return [new ZonedDateTimeRange(other._end, _end)];
            //  |----------------|
            //              |----|
            else if (_end == other._end)
                return [new ZonedDateTimeRange(_start, other._start)];
            //  |----------------|
            //        |----|
            else
                return [
                    new ZonedDateTimeRange(_start, other._start),
                    new ZonedDateTimeRange(other._end, _end)
                ];
        }
        else
        {
            //      |----------------|
            //   |----|
            if (other.Contains(_start))
                return [new ZonedDateTimeRange(other._end, _end)];
            //  |----------------|
            //                |----|
            else
                return [new ZonedDateTimeRange(_start, other._start)];
        }
    }

    public readonly LocalDateRange ToLocalDateRangeLeniently()
    {
        return new LocalDateRange(_start.Date, _end.TimeOfDay == LocalTime.Midnight ? _end.Date : _end.Date.PlusDays(1));
    }

    public readonly LocalDateRange ToLocalDateRangeStrict()
    {
        InvalidOperationException.ThrowUnless(Start.TimeOfDay == LocalTime.Midnight);
        InvalidOperationException.ThrowUnless(End.TimeOfDay == LocalTime.Midnight);

        return new LocalDateRange(_start.Date, _end.Date);
    }

    public readonly LocalDateTimeRange ToLocalDateTimeRange()
    {
        return new LocalDateTimeRange(_start.LocalDateTime, _end.LocalDateTime);
    }

    public readonly ZonedDateTimeRange WithZone(string timezone)
    {
        var zone = DateTimeZoneProviders.Tzdb[timezone];
        return WithZone(zone);
    }

    public readonly ZonedDateTimeRange WithZone(DateTimeZone dateTimeZone)
    {
        return new ZonedDateTimeRange(_start.WithZone(dateTimeZone), _end.WithZone(dateTimeZone));
    }

    public readonly ZonedDateTimeRange InUtc()
    {
        return WithZone(DateTimeZone.Utc);
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

    public readonly bool Equals(ZonedDateTimeRange other)
    {
        return _start == other._start && _end == other._end;
    }

    public static bool operator ==(ZonedDateTimeRange x, ZonedDateTimeRange y)
    {
        return x.Equals(y);
    }

    public static bool operator !=(ZonedDateTimeRange x, ZonedDateTimeRange y)
    {
        return !x.Equals(y);
    }

    public override readonly bool Equals(object? obj)
    {
        if (obj is not ZonedDateTimeRange)
            return false;

        return Equals((ZonedDateTimeRange)obj);
    }

    private readonly string GetDebuggerDisplay()
    {
        return ToString();
>>>>>>> After


namespace Ark.Tools.Nodatime;

[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct ZonedDateTimeRange
    : IEquatable<ZonedDateTimeRange>
{
    private readonly ZonedDateTime _start, _end;

    public ZonedDateTimeRange(ZonedDateTime start, ZonedDateTime end)
    {
        InvalidOperationException.ThrowUnless(ZonedDateTime.Comparer.Instant.Compare(start, end) < 0);
        InvalidOperationException.ThrowUnless(start.Zone.Equals(end.Zone));

        _start = start;
        _end = end;
    }
    public override readonly string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "Start:{0} | End:{1}", _start, _end);
    }


    public readonly ZonedDateTime Start { get { return _start; } }
    public readonly ZonedDateTime End { get { return _end; } }
    public readonly DateTimeZone Zone { get { return _start.Zone; } }

    public readonly bool Contains(ZonedDateTime ldt)
    {
        InvalidOperationException.ThrowUnless(ldt.Zone.Equals(Start.Zone));
        return ldt.ToInstant() >= _start.ToInstant() && ldt.ToInstant() < _end.ToInstant();
    }

    public readonly bool Contains(ZonedDateTimeRange other)
    {
        InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
        return other._start.ToInstant() >= _start.ToInstant() && other._end.ToInstant() <= _end.ToInstant();
    }

    public readonly bool Overlaps(ZonedDateTimeRange other)
    {
        InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
        return _start.ToInstant() < other._end.ToInstant() && _end.ToInstant() > other._start.ToInstant();
    }

    public readonly bool OverlapsOrContiguous(ZonedDateTimeRange other)
    {
        InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
        return Overlaps(other) || IsContiguous(other);
    }

    public readonly bool IsContiguous(ZonedDateTimeRange other)
    {
        InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
        return _start == other._end || _end == other._start;
    }
    public readonly ZonedDateTimeRange MergeOverlapsOrContiguous(ZonedDateTimeRange other)
    {
        InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
        InvalidOperationException.ThrowUnless(OverlapsOrContiguous(other));
        return new ZonedDateTimeRange(
              _start.ToInstant() < other._start.ToInstant() ? _start : other._start
            , _end.ToInstant() > other._end.ToInstant() ? _end : other._end
            );
    }

    public readonly ZonedDateTimeRange Merge(ZonedDateTimeRange other)
    {
        InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));
        return new ZonedDateTimeRange(
            _start.ToInstant() < other._start.ToInstant() ? _start : other._start,
            _end.ToInstant() > other._end.ToInstant() ? _end : other._end
            );
    }

    public readonly IEnumerable<ZonedDateTimeRange> Subtract(ZonedDateTimeRange other)
    {
        InvalidOperationException.ThrowUnless(other.Zone.Equals(Start.Zone));

        //      |------------|
        //  |--------------------|
        if (other.Contains(this))
            return Array.Empty<ZonedDateTimeRange>();
        //  |----------------|
        //                      |----|
        else if (!Overlaps(other))
            return [this];
        else if (Contains(other))
        {
            //  |----------------|
            //  |----|
            if (_start == other._start)
                return [new ZonedDateTimeRange(other._end, _end)];
            //  |----------------|
            //              |----|
            else if (_end == other._end)
                return [new ZonedDateTimeRange(_start, other._start)];
            //  |----------------|
            //        |----|
            else
                return [
                    new ZonedDateTimeRange(_start, other._start),
                    new ZonedDateTimeRange(other._end, _end)
                ];
        }
        else
        {
            //      |----------------|
            //   |----|
            if (other.Contains(_start))
                return [new ZonedDateTimeRange(other._end, _end)];
            //  |----------------|
            //                |----|
            else
                return [new ZonedDateTimeRange(_start, other._start)];
        }
    }

    public readonly LocalDateRange ToLocalDateRangeLeniently()
    {
        return new LocalDateRange(_start.Date, _end.TimeOfDay == LocalTime.Midnight ? _end.Date : _end.Date.PlusDays(1));
    }

    public readonly LocalDateRange ToLocalDateRangeStrict()
    {
        InvalidOperationException.ThrowUnless(Start.TimeOfDay == LocalTime.Midnight);
        InvalidOperationException.ThrowUnless(End.TimeOfDay == LocalTime.Midnight);

        return new LocalDateRange(_start.Date, _end.Date);
    }

    public readonly LocalDateTimeRange ToLocalDateTimeRange()
    {
        return new LocalDateTimeRange(_start.LocalDateTime, _end.LocalDateTime);
    }

    public readonly ZonedDateTimeRange WithZone(string timezone)
    {
        var zone = DateTimeZoneProviders.Tzdb[timezone];
        return WithZone(zone);
    }

    public readonly ZonedDateTimeRange WithZone(DateTimeZone dateTimeZone)
    {
        return new ZonedDateTimeRange(_start.WithZone(dateTimeZone), _end.WithZone(dateTimeZone));
    }

    public readonly ZonedDateTimeRange InUtc()
    {
        return WithZone(DateTimeZone.Utc);
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

    public readonly bool Equals(ZonedDateTimeRange other)
    {
        return _start == other._start && _end == other._end;
    }

    public static bool operator ==(ZonedDateTimeRange x, ZonedDateTimeRange y)
    {
        return x.Equals(y);
    }

    public static bool operator !=(ZonedDateTimeRange x, ZonedDateTimeRange y)
    {
        return !x.Equals(y);
    }

    public override readonly bool Equals(object? obj)
    {
        if (obj is not ZonedDateTimeRange)
            return false;

        return Equals((ZonedDateTimeRange)obj);
    }

    private readonly string GetDebuggerDisplay()
    {
        return ToString();
    }
}