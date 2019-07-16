namespace Ark.Tools.EventSourcing.Aggregates
{
	public abstract class AggregateState<TAggregate, TAggregateState> : IAggregateState//, IAuditableEntity
        where TAggregate : AggregateRoot<TAggregate, TAggregateState>, new()
        where TAggregateState : AggregateState<TAggregate, TAggregateState>, new()
    {
        public string Identifier { get; internal set; }
        public long Version { get; internal set; }
		//public Guid AuditId { get; set; }
	}



}
