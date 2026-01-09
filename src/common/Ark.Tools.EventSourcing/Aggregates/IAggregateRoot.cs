namespace Ark.Tools.EventSourcing.Aggregates;

public interface IAggregateRoot
{
    string Identifier { get; }
    long Version { get; }
    bool IsNew { get; }
}