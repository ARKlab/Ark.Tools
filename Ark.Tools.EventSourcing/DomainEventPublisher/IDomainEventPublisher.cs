using Ark.Tools.EventSourcing.Store;

using System.Threading.Tasks;

namespace Ark.Tools.EventSourcing.DomainEventPublisher
{
    public interface IDomainEventPublisher
    {
        Task PublishAsync(OutboxEvent outboxEvent);
    }
}