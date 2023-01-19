// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;
using EnsureThat;
using NodaTime;
using System;
using System.Collections.Generic;

namespace Ark.Tools.Nodatime
{
    public struct LocalDateRange
        : IEquatable<LocalDateRange>
    {
        private LocalDate _start, _end;

        public LocalDateRange(LocalDate start, LocalDate end)
        {
            Ensure.Comparable.IsLt(start, end, nameof(start));

            _start = start;
            _end = end;
        }
        public override string ToString()
        {
            return string.Format("Start:{0} | End:{1}", _start,_end);
        }


        public LocalDate Start
        {
            get { return _start; }
            set
            {
                _start = value;
            }
        }
        public LocalDate End
        {
            get { return _end; }
            set
            {
                _end = value;
            }
        }

        public bool Contains(LocalDate ld)
        {
            return ld >= _start && ld < _end;
        }

        public bool Contains(LocalDateTime ldt)
        {
            return ldt.Date >= _start && ldt.Date < End;
        }

        public bool Contains(LocalDateRange other)
        {
            return other._start >= _start && other._end <= _end;
        }

        public bool Overlaps(LocalDateRange other)
        {
            return _start < other._end && _end > other._start;
        }

        public bool OverlapsOrContiguous(LocalDateRange other)
        {
            return Overlaps(other) || IsContiguous(other);
        }

        public bool IsContiguous(LocalDateRange other)
        {
            return _start == other._end || _end == other._start;
        }

        public LocalDateRange MergeOverlapsOrContiguous(LocalDateRange other)
        {
            Ensure.Bool.IsTrue(OverlapsOrContiguous(other));
            return new LocalDateRange(_start.MinWith(other._start), _end.MaxWith(other._end));
        }

        public LocalDateRange Merge(LocalDateRange other)
        {
            return new LocalDateRange(_start.MinWith(other._start), _end.MaxWith(other._end));
        }

        public IEnumerable<LocalDateRange> Subtract(LocalDateRange other)
        {
            //      |------------|
            //  |--------------------|
            if (other.Contains(this))
                return Array.Empty<LocalDateRange>();
            //  |----------------|
            //                      |----|
            else if (!Overlaps(other))
                return new[] { this };
            else if (Contains(other))
            {
                //  |----------------|
                //  |----|
                if (_start == other._start)
                    return new[] { new LocalDateRange(other._end, _end) };
                //  |----------------|
                //              |----|
                else if (_end == other._end)
                    return new[] { new LocalDateRange(_start, other._start) };
                //  |----------------|
                //        |----|
                else
                    return new[] {
                        new LocalDateRange(_start, other._start),
                        new LocalDateRange(other._end, _end)
                    };
            }
            else
            {
                //      |----------------|
                //   |----|
                if (other.Contains(_start))
                    return new[] { new LocalDateRange(other._end, _end) };
                //  |----------------|
                //                |----|
                else
                    return new[] { new LocalDateRange(_start, other._start) };
            }
        }

        public LocalDateTimeRange ToLocalDateTimeRange()
        {
            return new LocalDateTimeRange(_start.AtMidnight(), _end.AtMidnight());
        }

        public ZonedDateTimeRange ToZonedDateTimeRange(string timezone)
        {
            return ToLocalDateTimeRange().ToZonedDateTimeRange(timezone);
        }

        public ZonedDateTimeRange ToZonedDateTimeRange(DateTimeZone dateTimeZone)
        {
            return ToLocalDateTimeRange().ToZonedDateTimeRange(dateTimeZone);
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

        public bool Equals(LocalDateRange other)
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

        public override bool Equals(object? obj)
        {
            if (obj is not LocalDateRange)
                return false;

            return Equals((LocalDateRange)obj);
        }
    }
}