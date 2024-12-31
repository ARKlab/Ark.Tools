using System.Threading.Tasks;

using Ark.Tools.EventSourcing.DomainEventPublisher;
using Ark.Tools.EventSourcing.Store;

using Rebus.Bus;

namespace Ark.Tools.DomainEventPublisher.Rebus
{
    public class RebusDomainEventPublisher : IDomainEventPublisher
    {
        private readonly IBus _bus;

        public RebusDomainEventPublisher(IBus bus)
        {
            _bus = bus;
        }

        public async Task PublishAsync(OutboxEvent @event)
        {
            await _bus.Publish(@event.GetEvent(), @event.Metadata).ConfigureAwait(false);
        }
    }
}
