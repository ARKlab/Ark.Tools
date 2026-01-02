// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Nodatime.Intervals;

using EnsureThat;

using NodaTime;

using System.Collections.Generic;

namespace Ark.Tools.Nodatime.TimeSeries
{
    public class UniformDateSequence
        : IEnumerable<Intervals.DateInterval>
    {
        private LocalDateRange _range;
        private DatePeriod _period;

        public UniformDateSequence(LocalDate start, LocalDate end, DatePeriod period, bool exact = true)
        {
            Ensure.Bool.IsTrue(start < end);
            Ensure.Bool.IsTrue(exact == true ? Intervals.DateInterval.StartOfInterval(start, period) == start : true);
            Ensure.Bool.IsTrue(exact == true ? Intervals.DateInterval.StartOfInterval(end, period) == end : true);

            var startOfEnd = Intervals.DateInterval.StartOfInterval(end, period);
            if (exact == false && startOfEnd < end)
                startOfEnd = new Intervals.DateInterval(startOfEnd, period).NextInterval().Date;

            _range = new LocalDateRange(
                Intervals.DateInterval.StartOfInterval(start, period),
                startOfEnd);
            _period = period;
        }

        public UniformDateSequence(LocalDateRange range, DatePeriod period, bool exact = true)
        {
            Ensure.Bool.IsTrue(exact == true ? Intervals.DateInterval.StartOfInterval(range.Start, period) == range.Start : true);
            Ensure.Bool.IsTrue(exact == true ? Intervals.DateInterval.StartOfInterval(range.End, period) == range.End : true);

            var startOfEnd = Intervals.DateInterval.StartOfInterval(range.End, period);
            if (exact == false && startOfEnd < range.End)
                startOfEnd = new Intervals.DateInterval(startOfEnd, period).NextInterval().Date;

            _range = new LocalDateRange(
                Intervals.DateInterval.StartOfInterval(range.Start, period),
                startOfEnd);
            _period = period;
        }

        public LocalDateRange Range { get { return _range; } }
        public DatePeriod Period { get { return _period; } }

        public void ChangePeriod(DatePeriod period)
        {
            _period = period;
        }

        public IEnumerator<Intervals.DateInterval> GetEnumerator()
        {
            var c = new Intervals.DateInterval(_range.Start, _period);
            var e = new Intervals.DateInterval(_range.End, _period);
            while (c < e)
            {
                yield return c;
                c = c.NextInterval();
            }
        }

        public IEnumerable<LocalDate> AsDateSequence()
        {
            var c = _range.Start;
            var e = _range.End;
            var incr = Intervals.DateInterval.GetIncrement(_period);
            while (c < e)
            {
                yield return c;
                c = c + incr;
            }
        }

        public IEnumerable<Intervals.DateInterval> AsIntervalSequence()
        {
            return this;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}