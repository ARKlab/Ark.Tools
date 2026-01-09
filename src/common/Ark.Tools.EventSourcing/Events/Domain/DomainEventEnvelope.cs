
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Events
{
    public sealed class DomainEventEnvelope : EventEnvelope<IDomainEvent>
    {
        public DomainEventEnvelope(IDomainEvent domainEvent, IMetadata metadata)
            : base(domainEvent, metadata)
        {
        }
=======
namespace Ark.Tools.EventSourcing.Events;

public sealed class DomainEventEnvelope : EventEnvelope<IDomainEvent>
{
    public DomainEventEnvelope(IDomainEvent domainEvent, IMetadata metadata)
        : base(domainEvent, metadata)
    {
>>>>>>> After
namespace Ark.Tools.EventSourcing.Events;

    public sealed class DomainEventEnvelope : EventEnvelope<IDomainEvent>
    {
        public DomainEventEnvelope(IDomainEvent domainEvent, IMetadata metadata)
            : base(domainEvent, metadata)
        {
        }
    }