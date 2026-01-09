using Ark.Tools.EventSourcing.DomainEventPublisher;
using Ark.Tools.EventSourcing.Store;

using Rebus.Bus;

using System.Threading.Tasks;

namespace Ark.Tools.DomainEventPublisher.Rebus;

public class RebusDomainEventPublisher : IDomainEventPublisher
{
    private readonly IBus _bus;

    public RebusDomainEventPublisher(IBus bus)
    {
        _bus = bus;
    }

    public async Task PublishAsync(OutboxEvent outboxEvent)
    {
        await _bus.Publish(outboxEvent.GetEvent(), outboxEvent.Metadata).ConfigureAwait(false);
    }
}
