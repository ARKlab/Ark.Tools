using System;
using Rebus.Time;
using NodaTime;

namespace Ark.Reference.Common
{
    public sealed class RebusIClock : IRebusTime
    {
        private readonly IClock _clock;

        public RebusIClock(IClock clock)
        {
            _clock = clock;
        }

        public DateTimeOffset Now => _clock.GetCurrentInstant().ToDateTimeOffset();
    }
}
