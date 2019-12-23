using Ark.Tools.Activity.Messages;
using NLog;
using System.Linq;
using System.Threading.Tasks;
using Rebus.Sagas;
using Rebus.Bus;
using EnsureThat;
using System;
using Rebus.Handlers;

namespace Ark.Tools.Activity.Processor
{

    public sealed class SliceActivitySaga
        : Saga<SliceActivitySagaData>
        , IAmInitiatedBy<SliceReady>
        , IAmInitiatedBy<Ark.Tasks.Messages.SliceReady>
		, IHandleMessages<CoolDownMessage>

	{
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly ISliceActivity _activity;
        private readonly IBus _bus;

        public SliceActivitySaga(ISliceActivity activity, IBus bus)
        {
            _activity = activity;
            _bus = bus;
        }

        public async Task Handle(SliceReady message)
        {
			_activity.Logger.Info("Slice {0} received dependency for resource {1}@{2}", message.ActivitySlice, message.Resource, message.ResourceSlice);
            var sourceDep = _activity.Dependencies.Single(x => x.Resource == message.Resource);
            
            if (IsNew)
            {
                Data.ActivitySlice = message.ActivitySlice;
                Data.MissingSlices = _activity.Dependencies.SelectMany(d => d.GetResourceSlices(Data.ActivitySlice).Select(x => new SliceReady() { ActivitySlice = Data.ActivitySlice, Resource = d.Resource, ResourceSlice = x })).ToList();
            }

            Ensure.Bool.IsTrue(Data.ActivitySlice == message.ActivitySlice);

            Data.MissingSlices.Remove(message);
            
            if (Data.MissingSlices.Count == 0)
            {
				if (_activity.CoolDown == null || Data.IsCoolDown == false)
				{
					await _process();
				}
				else if (Data.IsCoolDown && !Data.IsScheduled)
				{
					await _schedule(message);
				}
				else
				{
					_activity.Logger.Info($"Nothing to do for Slice { Data.ActivitySlice}");
				}
			}
            else
            {
                _activity.Logger.Info("Skipped materialization for slice {0}. Missing {1}", Data.ActivitySlice, string.Join(",", Data.MissingSlices.Select(m => string.Format("{0}@{1}", m.Resource, m.ResourceSlice))));
            }
        }

        public Task Handle(Tasks.Messages.SliceReady message)
        {
            return Handle(new SliceReady()
            {
                ActivitySlice = Slice.From(message.ActivitySlice.SliceStart),
                Resource = Resource.Create(message.Resource.Provider, message.Resource.Id),
                ResourceSlice = Slice.From(message.ResourceSlice.SliceStart)
            });
        }


		public async Task Handle(CoolDownMessage message)
		{
			_activity.Logger.Info($"Message Processed after CoolDown Time for Slice { Data.ActivitySlice}");
			await _process();
		}

		private async Task _process()
		{
			await _activity.Process(Data.ActivitySlice).ConfigureAwait(false);
			await _bus.Advanced.Topics.Publish(_activity.Resource.ToString(), new ResourceSliceReady()
			{
				Resource = _activity.Resource,
				Slice = Data.ActivitySlice
			});
			_activity.Logger.Info("Completed materialization for slice {0}.", Data.ActivitySlice);

			// Reset CD
			Data.IsScheduled = false;
			Data.CoolDownTill = null;

			if (_activity.CoolDown != null)
				Data.CoolDownTill = DateTimeOffset.UtcNow + _activity.CoolDown;
		}

		private async Task _schedule(SliceReady message)
		{
			var timeToWait = (Data.CoolDownTill.Value - DateTimeOffset.UtcNow);

			if (timeToWait.TotalSeconds > 0)
			{
				_activity.Logger.Info($"Message Deferred after {timeToWait.TotalSeconds} seconds for Slice { Data.ActivitySlice}");

				await _bus.DeferLocal(timeToWait, new CoolDownMessage()
				{
					ActivitySlice = Slice.From(message.ActivitySlice.SliceStart),
					Resource = Resource.Create(message.Resource.Provider, message.Resource.Id),
					ResourceSlice = Slice.From(message.ResourceSlice.SliceStart)
				});

				Data.IsScheduled = true;
			}
		}

		protected override void CorrelateMessages(ICorrelationConfig<SliceActivitySagaData> config)
        {
            config.Correlate<SliceReady>(m => m.ActivitySlice.ToString(), s => s.FormattedSliceStart);
            config.Correlate<Ark.Tasks.Messages.SliceReady>(m => m.ActivitySlice.ToString(), s => s.FormattedSliceStart);
			config.Correlate<CoolDownMessage>(m => m.ActivitySlice.ToString(), s => s.FormattedSliceStart);
		}
	}

}
