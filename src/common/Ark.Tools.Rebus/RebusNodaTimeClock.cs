using NodaTime;

using Rebus.Time;


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