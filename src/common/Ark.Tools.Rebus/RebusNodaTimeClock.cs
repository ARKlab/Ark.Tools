using NodaTime;

using Rebus.Time;

using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Rebus(net10.0)', Before:
namespace Ark.Tools.Rebus
{
    public sealed class RebusNodaTimeClock : IRebusTime
    {
        private readonly IClock _clock;

        public RebusNodaTimeClock(IClock clock)
        {
            _clock = clock;
        }

        public DateTimeOffset Now => _clock.GetCurrentInstant().ToDateTimeOffset();
    }


=======
namespace Ark.Tools.Rebus;

public sealed class RebusNodaTimeClock : IRebusTime
{
    private readonly IClock _clock;

    public RebusNodaTimeClock(IClock clock)
    {
        _clock = clock;
    }

    public DateTimeOffset Now => _clock.GetCurrentInstant().ToDateTimeOffset();
>>>>>>> After
    namespace Ark.Tools.Rebus;

    public sealed class RebusNodaTimeClock : IRebusTime
    {
        private readonly IClock _clock;

        public RebusNodaTimeClock(IClock clock)
        {
            _clock = clock;
        }

        public DateTimeOffset Now => _clock.GetCurrentInstant().ToDateTimeOffset();
    }