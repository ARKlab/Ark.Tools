namespace Ark.Tools.EventSourcing.Aggregates
{
	public abstract class AggregateState<TAggregateState, TAggregate> : IAggregateState
        where TAggregateState : AggregateState<TAggregateState, TAggregate>, new()
        where TAggregate : IAggregate
    {
        public string Identifier { get; internal set; }
        public long Version { get; internal set; }
	}



}
