namespace Ark.Tools.EventSourcing.Aggregates;

public interface IAggregateState
{
    string Identifier { get; }
    long Version { get; }
}