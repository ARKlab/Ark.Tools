using Ark.Tools.EventSourcing.Store;

using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.DomainEventPublisher
{
    public interface IDomainEventPublisher
    {
        Task PublishAsync(OutboxEvent outboxEvent);
    }
=======
namespace Ark.Tools.EventSourcing.DomainEventPublisher;

public interface IDomainEventPublisher
{
    Task PublishAsync(OutboxEvent outboxEvent);
>>>>>>> After


namespace Ark.Tools.EventSourcing.DomainEventPublisher;

public interface IDomainEventPublisher
{
    Task PublishAsync(OutboxEvent outboxEvent);
}