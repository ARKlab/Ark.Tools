using System;
using System.Runtime.Serialization;

namespace Ark.Tools.EventSourcing.Aggregates
{    
	public abstract class AggregateState<TAggregateState, TAggregate> : IAggregateState
        where TAggregateState : AggregateState<TAggregateState, TAggregate>, new()
        where TAggregate : IAggregate
    {
        internal bool _isRootManaged = false;
        internal string _identifier = string.Empty;
        internal long _version;

        public string Identifier 
        {
            get { return _identifier; }
            set {
                if (_isRootManaged)
                    throw new InvalidOperationException($"Cannot set Identifier when AggregateState is managed by an AggregateRoot");
                _identifier = value;
            }
        }
        public long Version
        {
            get { return _version; }
            set
            {
                if (_isRootManaged)
                    throw new InvalidOperationException($"Cannot set Version when AggregateState is managed by an AggregateRoot");
                _version = value;
            }
        }
    }
}
