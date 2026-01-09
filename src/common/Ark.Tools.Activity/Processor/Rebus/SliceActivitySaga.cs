using Ark.Tools.Activity.Messages;
using Ark.Tools.Core;

using Rebus.

using Rebus.Bus;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Activity(net10.0)', Before:
namespace Ark.Tools.Activity.Processor
{

    public sealed class SliceActivitySaga
        : Saga<SliceActivitySagaData>
        , IAmInitiatedBy<SliceReady>
        , IAmInitiatedBy<Ark.Tasks.Messages.SliceReady>
        , IHandleMessages<CoolDownMessage>

    {
        private readonly ISliceActivity _activity;
        private readonly IBus _bus;

        public SliceActivitySaga(ISliceActivity activity, IBus bus)
        {
            _activity = activity;
            _bus = bus;
        }

        public async Task Handle(SliceReady message)
        {
            _activity.Logger.Info("Slice {ActivitySlice} received dependency for resource {Resource}@{ResourceSlice}", message.ActivitySlice, message.Resource, message.ResourceSlice);
            var sourceDep = _activity.Dependencies.Single(x => x.Resource == message.Resource);

            if (IsNew)
            {
                Data.ActivitySlice = message.ActivitySlice;
                Data.MissingSlices = _activity.Dependencies.SelectMany(d => d.GetResourceSlices(Data.ActivitySlice).Select(x => new SliceReady() { ActivitySlice = Data.ActivitySlice, Resource = d.Resource, ResourceSlice = x })).ToList();
            }

            InvalidOperationException.ThrowUnless(Data.ActivitySlice == message.ActivitySlice);

            Data.MissingSlices.Remove(message);

            if (Data.MissingSlices.Count == 0)
            {
                if (_activity.CoolDown == null || Data.IsCoolDown == false)
                {
                    await _process().ConfigureAwait(false);
                }
                else if (Data.IsCoolDown && !Data.IsScheduled)
                {
                    await _schedule(message).ConfigureAwait(false);
                }
                else
                {
                    _activity.Logger.Info("Nothing to do for Slice {ActivitySlice}", Data.ActivitySlice);
                }
            }
            else
            {
                _activity.Logger.Info("Skipped materialization for slice {ActivitySlice}. Missing {MissingSlices}", Data.ActivitySlice, string.Join(",", Data.MissingSlices.Select(m => string.Format("{0}@{1}", m.Resource, m.ResourceSlice))));
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
            _activity.Logger.Info("Message Processed after CoolDown Time for Slice {ActivitySlice}", Data.ActivitySlice);
            await _process().ConfigureAwait(false);
        }

        private async Task _process()
        {
            await _activity.Process(Data.ActivitySlice).ConfigureAwait(false);
            await _bus.Advanced.Topics.Publish(_activity.Resource.ToString(), new ResourceSliceReady()
            {
                Resource = _activity.Resource,
                Slice = Data.ActivitySlice
            }).ConfigureAwait(false);
            _activity.Logger.Info("Completed materialization for slice {ActivitySlice}.", Data.ActivitySlice);

            // Reset CD
            Data.IsScheduled = false;
            Data.CoolDownTill = null;

            if (_activity.CoolDown != null)
                Data.CoolDownTill = DateTimeOffset.UtcNow + _activity.CoolDown;
        }

        private async Task _schedule(SliceReady message)
        {
            var timeToWait = (Data.CoolDownTill - DateTimeOffset.UtcNow);

            if (timeToWait != null && timeToWait.Value.TotalSeconds > 0)
            {
                _activity.Logger.Info("Message Deferred after {TimeToWait}s seconds for Slice {ActivitySlice}", timeToWait.Value.TotalSeconds, Data.ActivitySlice);

                await _bus.DeferLocal(timeToWait.Value, new CoolDownMessage()
                {
                    ActivitySlice = Slice.From(message.ActivitySlice.SliceStart),
                    Resource = Resource.Create(message.Resource.Provider, message.Resource.Id),
                    ResourceSlice = Slice.From(message.ResourceSlice.SliceStart)
                }).ConfigureAwait(false);

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
=======
namespace Ark.Tools.Activity.Processor;


public sealed class SliceActivitySaga
    : Saga<SliceActivitySagaData>
    , IAmInitiatedBy<SliceReady>
    , IAmInitiatedBy<Ark.Tasks.Messages.SliceReady>
    , IHandleMessages<CoolDownMessage>

{
    private readonly ISliceActivity _activity;
    private readonly IBus _bus;

    public SliceActivitySaga(ISliceActivity activity, IBus bus)
    {
        _activity = activity;
        _bus = bus;
    }

    public async Task Handle(SliceReady message)
    {
        _activity.Logger.Info("Slice {ActivitySlice} received dependency for resource {Resource}@{ResourceSlice}", message.ActivitySlice, message.Resource, message.ResourceSlice);
        var sourceDep = _activity.Dependencies.Single(x => x.Resource == message.Resource);

        if (IsNew)
        {
            Data.ActivitySlice = message.ActivitySlice;
            Data.MissingSlices = _activity.Dependencies.SelectMany(d => d.GetResourceSlices(Data.ActivitySlice).Select(x => new SliceReady() { ActivitySlice = Data.ActivitySlice, Resource = d.Resource, ResourceSlice = x })).ToList();
        }

        InvalidOperationException.ThrowUnless(Data.ActivitySlice == message.ActivitySlice);

        Data.MissingSlices.Remove(message);

        if (Data.MissingSlices.Count == 0)
        {
            if (_activity.CoolDown == null || Data.IsCoolDown == false)
            {
                await _process().ConfigureAwait(false);
            }
            else if (Data.IsCoolDown && !Data.IsScheduled)
            {
                await _schedule(message).ConfigureAwait(false);
            }
            else
            {
                _activity.Logger.Info("Nothing to do for Slice {ActivitySlice}", Data.ActivitySlice);
            }
        }
        else
        {
            _activity.Logger.Info("Skipped materialization for slice {ActivitySlice}. Missing {MissingSlices}", Data.ActivitySlice, string.Join(",", Data.MissingSlices.Select(m => string.Format("{0}@{1}", m.Resource, m.ResourceSlice))));
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
        _activity.Logger.Info("Message Processed after CoolDown Time for Slice {ActivitySlice}", Data.ActivitySlice);
        await _process().ConfigureAwait(false);
    }

    private async Task _process()
    {
        await _activity.Process(Data.ActivitySlice).ConfigureAwait(false);
        await _bus.Advanced.Topics.Publish(_activity.Resource.ToString(), new ResourceSliceReady()
        {
            Resource = _activity.Resource,
            Slice = Data.ActivitySlice
        }).ConfigureAwait(false);
        _activity.Logger.Info("Completed materialization for slice {ActivitySlice}.", Data.ActivitySlice);

        // Reset CD
        Data.IsScheduled = false;
        Data.CoolDownTill = null;

        if (_activity.CoolDown != null)
            Data.CoolDownTill = DateTimeOffset.UtcNow + _activity.CoolDown;
    }

    private async Task _schedule(SliceReady message)
    {
        var timeToWait = (Data.CoolDownTill - DateTimeOffset.UtcNow);

        if (timeToWait != null && timeToWait.Value.TotalSeconds > 0)
        {
            _activity.Logger.Info("Message Deferred after {TimeToWait}s seconds for Slice {ActivitySlice}", timeToWait.Value.TotalSeconds, Data.ActivitySlice);

            await _bus.DeferLocal(timeToWait.Value, new CoolDownMessage()
            {
                ActivitySlice = Slice.From(message.ActivitySlice.SliceStart),
                Resource = Resource.Create(message.Resource.Provider, message.Resource.Id),
                ResourceSlice = Slice.From(message.ResourceSlice.SliceStart)
            }).ConfigureAwait(false);

            Data.IsScheduled = true;
        }
    }

    protected override void CorrelateMessages(ICorrelationConfig<SliceActivitySagaData> config)
    {
        config.Correlate<SliceReady>(m => m.ActivitySlice.ToString(), s => s.FormattedSliceStart);
        config.Correlate<Ark.Tasks.Messages.SliceReady>(m => m.ActivitySlice.ToString(), s => s.FormattedSliceStart);
        config.Correlate<CoolDownMessage>(m => m.ActivitySlice.ToString(), s => s.FormattedSliceStart);
    }
>>>>>>> After
    Handlers;
using Rebus.Sagas;

using System;
using System.Linq;
using System.Threading.Tasks;

namespace Ark.Tools.Activity.Processor;


    public sealed class SliceActivitySaga
        : Saga<SliceActivitySagaData>
        , IAmInitiatedBy<SliceReady>
        , IAmInitiatedBy<Ark.Tasks.Messages.SliceReady>
        , IHandleMessages<CoolDownMessage>

    {
        private readonly ISliceActivity _activity;
        private readonly IBus _bus;

        public SliceActivitySaga(ISliceActivity activity, IBus bus)
        {
            _activity = activity;
            _bus = bus;
        }

        public async Task Handle(SliceReady message)
        {
            _activity.Logger.Info("Slice {ActivitySlice} received dependency for resource {Resource}@{ResourceSlice}", message.ActivitySlice, message.Resource, message.ResourceSlice);
            var sourceDep = _activity.Dependencies.Single(x => x.Resource == message.Resource);

            if (IsNew)
            {
                Data.ActivitySlice = message.ActivitySlice;
                Data.MissingSlices = _activity.Dependencies.SelectMany(d => d.GetResourceSlices(Data.ActivitySlice).Select(x => new SliceReady() { ActivitySlice = Data.ActivitySlice, Resource = d.Resource, ResourceSlice = x })).ToList();
            }

            InvalidOperationException.ThrowUnless(Data.ActivitySlice == message.ActivitySlice);

            Data.MissingSlices.Remove(message);

            if (Data.MissingSlices.Count == 0)
            {
                if (_activity.CoolDown == null || Data.IsCoolDown == false)
                {
                    await _process().ConfigureAwait(false);
                }
                else if (Data.IsCoolDown && !Data.IsScheduled)
                {
                    await _schedule(message).ConfigureAwait(false);
                }
                else
                {
                    _activity.Logger.Info("Nothing to do for Slice {ActivitySlice}", Data.ActivitySlice);
                }
            }
            else
            {
                _activity.Logger.Info("Skipped materialization for slice {ActivitySlice}. Missing {MissingSlices}", Data.ActivitySlice, string.Join(",", Data.MissingSlices.Select(m => string.Format("{0}@{1}", m.Resource, m.ResourceSlice))));
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
            _activity.Logger.Info("Message Processed after CoolDown Time for Slice {ActivitySlice}", Data.ActivitySlice);
            await _process().ConfigureAwait(false);
        }

        private async Task _process()
        {
            await _activity.Process(Data.ActivitySlice).ConfigureAwait(false);
            await _bus.Advanced.Topics.Publish(_activity.Resource.ToString(), new ResourceSliceReady()
            {
                Resource = _activity.Resource,
                Slice = Data.ActivitySlice
            }).ConfigureAwait(false);
            _activity.Logger.Info("Completed materialization for slice {ActivitySlice}.", Data.ActivitySlice);

            // Reset CD
            Data.IsScheduled = false;
            Data.CoolDownTill = null;

            if (_activity.CoolDown != null)
                Data.CoolDownTill = DateTimeOffset.UtcNow + _activity.CoolDown;
        }

        private async Task _schedule(SliceReady message)
        {
            var timeToWait = (Data.CoolDownTill - DateTimeOffset.UtcNow);

            if (timeToWait != null && timeToWait.Value.TotalSeconds > 0)
            {
                _activity.Logger.Info("Message Deferred after {TimeToWait}s seconds for Slice {ActivitySlice}", timeToWait.Value.TotalSeconds, Data.ActivitySlice);

                await _bus.DeferLocal(timeToWait.Value, new CoolDownMessage()
                {
                    ActivitySlice = Slice.From(message.ActivitySlice.SliceStart),
                    Resource = Resource.Create(message.Resource.Provider, message.Resource.Id),
                    ResourceSlice = Slice.From(message.ResourceSlice.SliceStart)
                }).ConfigureAwait(false);

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