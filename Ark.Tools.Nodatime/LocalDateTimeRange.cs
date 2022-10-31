// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;
using EnsureThat;
using NodaTime;
using System;
using System.Collections.Generic;

namespace Ark.Tools.Nodatime
{
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
        public override string ToString()
        {
            return string.Format("Start:{0} | End:{1}", _start, _end);
        }


        public LocalDateTime Start
        {
            get { return _start; }
            set
            {
                _start = value;
            }
        }

        public LocalDateTime End
        {
            get { return _end; }
            set
            {
                _end = value;
            }
        }

        public bool Contains(LocalDateTime ldt)
        {
            return ldt >= _start && ldt < _end;
        }

        public bool Contains(LocalDateTimeRange other)
        {
            return other._start >= _start && other._end <= _end;
        }

        public bool Overlaps(LocalDateTimeRange other)
        {
            return _start < other._end && _end > other._start;
        }

        public bool OverlapsOrContiguous(LocalDateTimeRange other)
        {
            return Overlaps(other) || IsContiguous(other);
        }

        public bool IsContiguous(LocalDateTimeRange other)
        {
            return _start == other._end || _end == other._start;
        }

        public LocalDateTimeRange MergeOverlapsOrContiguous(LocalDateTimeRange other)
        {
            Ensure.Bool.IsTrue(OverlapsOrContiguous(other));
            return new LocalDateTimeRange(_start.MinWith(other._start), _end.MaxWith(other._end));
        }

        public LocalDateTimeRange Merge(LocalDateTimeRange other)
        {
            return new LocalDateTimeRange(_start.MinWith(other._start), _end.MaxWith(other._end));
        }

        public IEnumerable<LocalDateTimeRange> Subtract(LocalDateTimeRange other)
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

        public LocalDateRange ToLocalDateRangeLeniently()
        {
            return new LocalDateRange(_start.Date, _end.TimeOfDay == LocalTime.Midnight ? _end.Date : _end.Date.PlusDays(1));
        }

        public LocalDateRange ToLocalDateRangeStrict()
        {
            Ensure.Bool.IsTrue(Start.TimeOfDay == LocalTime.Midnight);
            Ensure.Bool.IsTrue(End.TimeOfDay == LocalTime.Midnight);

            return new LocalDateRange(_start.Date, _end.Date);
        }

        public ZonedDateTimeRange ToZonedDateTimeRange(string timezone)
        {
            return ToZonedDateTimeRange(DateTimeZoneProviders.Tzdb[timezone]);
        }

        public ZonedDateTimeRange ToZonedDateTimeRange(DateTimeZone dateTimeZone)
        {
            return new ZonedDateTimeRange(_start.InZoneLeniently(dateTimeZone), _end.InZoneLeniently(dateTimeZone));
        }

        public ZonedDateTimeRange InZone(string timezone)
        {
            return ToZonedDateTimeRange(timezone);
        }

        public ZonedDateTimeRange InZone(DateTimeZone dateTimeZone)
        {
            return ToZonedDateTimeRange(dateTimeZone);
        }

        public ZonedDateTimeRange InUtc()
        {
            return ToZonedDateTimeRange(DateTimeZone.Utc);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 7243;
                hash = hash * 92821 + Start.GetHashCode();
                hash = hash * 92821 + End.GetHashCode();
                return hash;
            }
        }

        public bool Equals(LocalDateTimeRange other)
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

        public override bool Equals(object obj)
        {
            if (!(obj is LocalDateTimeRange))
                return false;

            return Equals((LocalDateTimeRange)obj);
        }
    }
}