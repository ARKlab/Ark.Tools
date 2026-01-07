// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
using System;
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Nodatime.Intervals;


using NodaTime;

using System.Collections.Generic;

namespace Ark.Tools.Nodatime.TimeSeries
{
    public class UniformTimeSequence
        : IEnumerable<TimeInterval>
    {
        private readonly ZonedDateTimeRange _range;
        private TimePeriod _period;

        public UniformTimeSequence(ZonedDateTime start, ZonedDateTime end, TimePeriod period, bool exact = true)
        {
            if (!(start.Zone.Equals(end.Zone))) { throw new InvalidOperationException("Condition failed"); }
            if (!(ZonedDateTime.Comparer.Instant.Compare(start, end) < 0)) { throw new InvalidOperationException("Condition failed"); }
            if (!(exact == true ? TimeInterval.StartOfInterval(start, period) == start : true)) { throw new InvalidOperationException("Condition failed"); }
            if (!(exact == true ? TimeInterval.StartOfInterval(end, period) == end : true)) { throw new InvalidOperationException("Condition failed"); }

            _range = new ZonedDateTimeRange(
                TimeInterval.StartOfInterval(start, period),
                TimeInterval.StartOfInterval(end, period));
            _period = period;
        }

        public UniformTimeSequence(ZonedDateTimeRange range, TimePeriod period, bool exact = true)
        {
            if (!(exact == true ? TimeInterval.StartOfInterval(range.Start, period) == range.Start : true)) { throw new InvalidOperationException("Condition failed"); }
            if (!(exact == true ? TimeInterval.StartOfInterval(range.End, period) == range.End : true)) { throw new InvalidOperationException("Condition failed"); }

            _range = new ZonedDateTimeRange(
                TimeInterval.StartOfInterval(range.Start, period),
                TimeInterval.StartOfInterval(range.End, period));
            _period = period;
        }

        public UniformTimeSequence(LocalDateTimeRange range, TimePeriod period, bool exact = true)
        {
            if (!(exact == true ? TimeInterval.StartOfInterval(range.Start, period) == range.Start : true)) { throw new InvalidOperationException("Condition failed"); }
            if (!(exact == true ? TimeInterval.StartOfInterval(range.End, period) == range.End : true)) { throw new InvalidOperationException("Condition failed"); }

            _range = new ZonedDateTimeRange(
                TimeInterval.StartOfInterval(range.Start.InUtc(), period),
                TimeInterval.StartOfInterval(range.End.InUtc(), period));
            _period = period;
        }

        public UniformTimeSequence(LocalDateTime start, LocalDateTime end, TimePeriod period, bool exact = true)
        {
            if (!(start < end)) { throw new InvalidOperationException("Condition failed: start < end"); }
            if (!(exact == true ? TimeInterval.StartOfInterval(start, period) == start : true)) { throw new InvalidOperationException("Condition failed"); }
            if (!(exact == true ? TimeInterval.StartOfInterval(end, period) == end : true)) { throw new InvalidOperationException("Condition failed"); }

            _range = new ZonedDateTimeRange(
                TimeInterval.StartOfInterval(start.InUtc(), period),
                TimeInterval.StartOfInterval(end.InUtc(), period));
            _period = period;
        }

        public ZonedDateTimeRange Range { get { return _range; } }
        public TimePeriod Period { get { return _period; } }

        public void ChangePeriod(TimePeriod period)
        {
            _period = period;
        }

        public IEnumerable<ZonedDateTime> AsTimeSequence()
        {
            var c = _range.Start;
            var e = _range.End;
            var incr = TimeInterval.GetIncrement(_period);
            while (c.ToInstant() < e.ToInstant())
            {
                yield return c;
                c = c + incr;
            }
        }

        public IEnumerable<TimeInterval> AsIntervalSequence()
        {
            return this;
        }

        public IEnumerator<TimeInterval> GetEnumerator()
        {
            var c = new TimeInterval(_range.Start, _period);
            var e = new TimeInterval(_range.End, _period);
            while (c < e)
            {
                yield return c;
                c = c.NextInterval();
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }


    }
}