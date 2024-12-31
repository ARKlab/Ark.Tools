using Ark.Tools.Activity;
using Ark.Tools.Activity.Processor;

using NodaTime;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TestReceiver
{
    public interface TestReceiver_Config
    {
        string ActivitySqlConnectionString { get; }
    }

    public class TestReceiver_Activity : CalendarSliceActivity
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly TestReceiver_Config _config;

        public TestReceiver_Activity(TestReceiver_Config config)
        {
            _config = config;
            Console.WriteLine($"********** Now {DateTimeOffset.UtcNow} ********");
        }

        public override Ark.Tools.Activity.ResourceDependency[] Dependencies
        {
            get
            {
                return [
                    ResourceDependency.OneSlice("test", "first", activitySlice => activitySlice),
                ];
            }
        }


        protected override IEnumerable<Ark.Tools.Activity.Slice> _generateCalendar()
        {
            var current = Slice.From(new LocalDate(2012, 1, 1), "CET");
            while (current.SliceStart.Year < 2020)
            {
                yield return current;
                current = current.MoveDays(1);
            }
        }

        public override Resource Resource { get; } = new Resource("TestReceiver", "TestReceiver_Activity");

        //public override TimeSpan? CoolDown { get; } = new TimeSpan(0, 2, 0);

        public override NLog.ILogger Logger
        {
            get { return _logger; }
        }

        public override TimeSpan? CoolDown => null;

        public override async Task Process(Ark.Tools.Activity.Slice activitySlice)
        {
            await Task.Delay(new TimeSpan(0, 0, 5));

            Console.WriteLine($"** Process Started Now {DateTimeOffset.UtcNow}**");
            throw new TimeoutException("Exception!");

            //await Task.FromResult(true);
        }

    }
}