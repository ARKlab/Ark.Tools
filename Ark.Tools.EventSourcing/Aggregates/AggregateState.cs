using System.Runtime.Serialization;

namespace Ark.Tools.EventSourcing.Aggregates
{
    [DataContract]
	public abstract class AggregateState<TAggregateState, TAggregate> : IAggregateState
        where TAggregateState : AggregateState<TAggregateState, TAggregate>, new()
        where TAggregate : IAggregate
    {
        [DataMember]
        public string Identifier { get; internal set; }
        [DataMember]
        public long Version { get; internal set; }
	}
}
