namespace Ark.Tools.EventSourcing.Events
{
    public sealed class DomainEventEnvelope : EventEnvelope<IDomainEvent>
    {
        public DomainEventEnvelope(IDomainEvent domainEvent, IMetadata metadata)
            : base(domainEvent, metadata)
        {
        }
    }
}
