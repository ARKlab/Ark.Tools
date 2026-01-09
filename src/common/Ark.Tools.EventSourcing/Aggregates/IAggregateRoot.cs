
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Aggregates
{
    public interface IAggregateRoot
    {
        string Identifier { get; }
        long Version { get; }
        bool IsNew { get; }
    }
=======
namespace Ark.Tools.EventSourcing.Aggregates;

public interface IAggregateRoot
{
    string Identifier { get; }
    long Version { get; }
    bool IsNew { get; }
>>>>>>> After
    namespace Ark.Tools.EventSourcing.Aggregates;

    public interface IAggregateRoot
    {
        string Identifier { get; }
        long Version { get; }
        bool IsNew { get; }
    }