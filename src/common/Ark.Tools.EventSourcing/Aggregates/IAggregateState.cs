
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Aggregates
{
    public interface IAggregateState
    {
        string Identifier { get; }
        long Version { get; }
    }
=======
namespace Ark.Tools.EventSourcing.Aggregates;

public interface IAggregateState
{
    string Identifier { get; }
    long Version { get; }
>>>>>>> After
namespace Ark.Tools.EventSourcing.Aggregates;

public interface IAggregateState
{
    string Identifier { get; }
    long Version { get; }
}