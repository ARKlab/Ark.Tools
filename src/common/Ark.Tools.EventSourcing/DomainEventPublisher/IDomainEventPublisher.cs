using Ark.Tools.EventSourcing.Store;


namespace Ark.Tools.EventSourcing.DomainEventPublisher;

public interface IDomainEventPublisher
{
    Task PublishAsync(OutboxEvent outboxEvent);
}