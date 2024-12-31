// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;
using EnsureThat;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Ark.Tools.Nodatime
{
    [StructLayout(LayoutKind.Auto)]
    public struct LocalDateTimeRange
        : IEquatable<LocalDateTimeRange>
    {
        private LocalDateTime _start, _end;

        public LocalDateTimeRange(LocalDateTime start, LocalDateTime end)
        {
            Ensure.Comparable.IsLt(start, end, nameof(start));

            _start = start;
            _end = end;
        }

        public LocalDateTimeRange(DateTime start, DateTime end)
        {
            Ensure.Comparable.IsLt(start, end, nameof(start));

            _start = LocalDateTime.FromDateTime(start);
            _end = LocalDateTime.FromDateTime(end);
        }
        public override readonly string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "Start:{0} | End:{1}", _start, _end);
        }


        public LocalDateTime Start
        {
            readonly get { return _start; }
            set
            {
                _start = value;
            }
        }

        public LocalDateTime End
        {
            readonly get { return _end; }
            set
            {
                _end = value;
            }
        }

        public readonly bool Contains(LocalDateTime ldt)
        {
            return ldt >= _start && ldt < _end;
        }

        public readonly bool Contains(LocalDateTimeRange other)
        {
            return other._start >= _start && other._end <= _end;
        }

        public readonly bool Overlaps(LocalDateTimeRange other)
        {
            return _start < other._end && _end > other._start;
        }

        public readonly bool OverlapsOrContiguous(LocalDateTimeRange other)
        {
            return Overlaps(other) || IsContiguous(other);
        }

        public readonly bool IsContiguous(LocalDateTimeRange other)
        {
            return _start == other._end || _end == other._start;
        }

        public readonly LocalDateTimeRange MergeOverlapsOrContiguous(LocalDateTimeRange other)
        {
            Ensure.Bool.IsTrue(OverlapsOrContiguous(other));
            return new LocalDateTimeRange(_start.MinWith(other._start), _end.MaxWith(other._end));
        }

        public readonly LocalDateTimeRange Merge(LocalDateTimeRange other)
        {
            return new LocalDateTimeRange(_start.MinWith(other._start), _end.MaxWith(other._end));
        }

        public readonly IEnumerable<LocalDateTimeRange> Subtract(LocalDateTimeRange other)
        {
            //      |------------|
            //  |--------------------|
            if (other.Contains(this))
                return Array.Empty<LocalDateTimeRange>();
            //  |----------------|
            //                      |----|
            else if (!Overlaps(other))
                return new[] { this };
            else if (Contains(other))
            {
                //  |----------------|
                //  |----|
                if (_start == other._start)
                    return new[] { new LocalDateTimeRange(other._end, _end) };
                //  |----------------|
                //              |----|
                else if (_end == other._end)
                    return new[] { new LocalDateTimeRange(_start, other._start) };
                //  |----------------|
                //        |----|
                else
                    return new[] {
                        new LocalDateTimeRange(_start, other._start),
                        new LocalDateTimeRange(other._end, _end)
                    };
            }
            else
            {
                //      |----------------|
                //   |----|
                if (other.Contains(_start))
                    return new[] { new LocalDateTimeRange(other._end, _end) };
                //  |----------------|
                //                |----|
                else
                    return new[] { new LocalDateTimeRange(_start, other._start) };
            }
        }

        public readonly LocalDateRange ToLocalDateRangeLeniently()
        {
            return new LocalDateRange(_start.Date, _end.TimeOfDay == LocalTime.Midnight ? _end.Date : _end.Date.PlusDays(1));
        }

        public readonly LocalDateRange ToLocalDateRangeStrict()
        {
            Ensure.Bool.IsTrue(Start.TimeOfDay == LocalTime.Midnight);
            Ensure.Bool.IsTrue(End.TimeOfDay == LocalTime.Midnight);

            return new LocalDateRange(_start.Date, _end.Date);
        }

        public readonly ZonedDateTimeRange ToZonedDateTimeRange(string timezone)
        {
            return ToZonedDateTimeRange(DateTimeZoneProviders.Tzdb[timezone]);
        }

        public readonly ZonedDateTimeRange ToZonedDateTimeRange(DateTimeZone dateTimeZone)
        {
            return new ZonedDateTimeRange(_start.InZoneLeniently(dateTimeZone), _end.InZoneLeniently(dateTimeZone));
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

        public readonly bool Equals(LocalDateTimeRange other)
        {
            return _start == other._start && _end == other._end;
        }

        public static bool operator ==(LocalDateTimeRange x, LocalDateTimeRange y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(LocalDateTimeRange x, LocalDateTimeRange y)
        {
            return !x.Equals(y);
        }

        public override readonly bool Equals(object? obj)
        {
            if (obj is not LocalDateTimeRange)
                return false;

            return Equals((LocalDateTimeRange)obj);
        }
    }
}