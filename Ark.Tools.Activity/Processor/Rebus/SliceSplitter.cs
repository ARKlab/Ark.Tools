using System.Threading.Tasks;
using Ark.Tools.Activity.Messages;
using Rebus.Handlers;
using Rebus.Bus;
using System.Linq;

using NLog;
using EnsureThat;

namespace Ark.Tools.Activity.Processor
{
    public class SliceSplitter : IHandleMessages<ResourceSliceReady>
    {
        private readonly ISliceActivity _activity;
        private readonly IBus _bus;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public SliceSplitter(ISliceActivity activity, IBus bus)
        {
            EnsureArg.IsNotNull(activity);
            EnsureArg.IsNotNull(bus);

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
            } else
            {
                _logger.Warn("Received an ResourceSliceReady event for the resource {0} that is not a dependency. Removing subscription to the resource.", e.Resource);
                return _bus.Advanced.Topics.Unsubscribe(e.Resource.ToString());
            }
        }
    }
}