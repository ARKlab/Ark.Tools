using NodaTime;
using NodaTime.Testing;

using System;
using System.Linq;

using Reqnroll;


namespace Ark.Reference.Core.Tests.Init
{
    [Binding]
    public class MockIClock : Steps
    {
        private static readonly FakeClock _fakeClock = new(SystemClock.Instance.GetCurrentInstant());
        private static readonly SwappableClock _swappableClock = new();

        public static IClock FakeClock => _swappableClock;
        public static bool IsClockFake => _swappableClock.Clock == _fakeClock;

        [BeforeScenario(Order = int.MinValue)]
        public void SetIClock(ScenarioContext sctx, FeatureContext fctx)
        {
            _fakeClock.AutoAdvance = Duration.Zero;
            ScenarioContext.ScenarioContainer.RegisterInstanceAs<IClock>(FakeClock);
            GivenTheCurrentInstantIs(SystemClock.Instance.GetCurrentInstant().WithOffset(Offset.FromHours(2)));

            //        this is needed for Rebus as sometime we need a normal Defer, sometime we need a 'Fake' defer.
            //        we should apply this fake-clock swapping 'only for rebus' and not for the entire applications
            if (sctx.ScenarioInfo.Tags.Concat(fctx.FeatureInfo.Tags).Any(x => x == "UseFakeClock"))
                _swappableClock.Clock = _fakeClock;
            else
                _swappableClock.Clock = SystemClock.Instance;
        }

        [Given(@"the current time is '(.*)'")]
        public static void GivenTheCurrentInstantIs(OffsetDateTime offSet)
        {
            _swappableClock.Clock = _fakeClock;
            _fakeClock.Reset(offSet.ToInstant());
            _fakeClock.AutoAdvance = Duration.FromMilliseconds(1000);
        }

        [Given(@"the current time is fixed at '(.*)'")]
        public void GivenTheCurrentInstantIsFixedAt(OffsetDateTime offSet)
        {
            _swappableClock.Clock = _fakeClock;
            _fakeClock.Reset(offSet.ToInstant());
            _fakeClock.AutoAdvance = Duration.Zero;
        }

        [Given(@"the time will auto-increment")]
        public static void GivenTheClockAutoIncrement()
        {
            if (_swappableClock.Clock != _fakeClock) throw new InvalidOperationException("Current scenario is not using the FakeClock");
            _fakeClock.AutoAdvance = Duration.FromMilliseconds(1000);
        }
    }


    sealed class SwappableClock : IClock
    {
        public IClock Clock { get; set; } = SystemClock.Instance;

        public Instant GetCurrentInstant()
        {
            return Clock.GetCurrentInstant();
        }
    }


}
