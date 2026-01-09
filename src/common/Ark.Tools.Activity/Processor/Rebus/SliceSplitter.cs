
using NLog;
using System;

using Rebus.Bus;
using Rebus.Handlers;

using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using ResourceSliceReady = Ark.Tools.Activity.Messages.ResourceSliceReady;
using SliceReady = Ark.Tools.Activity.Messages.SliceReady;

namespace Ark.Tools.Activity.Processor;

public class SliceSplitter
    : IHandleMessages<ResourceSliceReady>
    , IHandleMessages<Ark.Tasks.Messages.ResourceSliceReady>
{
    private readonly ISliceActivity _activity;
    private readonly IBus _bus;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public SliceSplitter(ISliceActivity activity, IBus bus)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(bus);

        _activity = activity;
        _bus = bus;
    }

    public Task Handle(ResourceSliceReady e)
    {
        // check if the resource is still our dependency. If not, unsubscribe
        if (_activity.Dependencies.Select(x => x.Resource).Contains(e.Resource))
        {
            var impactedSlices = _activity.ImpactedSlices(e.Resource, e.Slice);
            // Contract.Assume(Contract.ForAll(impactedSlices, s => _activity.Dependencies.Where(d => d.Resource == e.Resource).SelectMany(d => d.GetResourceSlices(s)).Any(x => x == e.Slice)));
            return Task.WhenAll(impactedSlices
                .Select(s => _bus.SendLocal(
                    new SliceReady()
                    {
                        Resource = e.Resource,
                        ActivitySlice = s,
                        ResourceSlice = e.Slice
                    })
                ));
        }
        else
        {
            _logger.Warn(CultureInfo.InvariantCulture, "Received an ResourceSliceReady event for the resource {Resource} that is not a dependency. Removing subscription to the resource.", e.Resource);
            return _bus.Advanced.Topics.Unsubscribe(e.Resource.ToString());
        }
    }

    public Task Handle(Tasks.Messages.ResourceSliceReady message)
    {
        return Handle(new ResourceSliceReady
        {
            Resource = Resource.Create(message.Resource.Provider, message.Resource.Id),
            Slice = Slice.From(message.Slice.SliceStart)
        });
    }
}
