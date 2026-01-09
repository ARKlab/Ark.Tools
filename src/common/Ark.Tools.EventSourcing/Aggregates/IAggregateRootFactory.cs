
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Aggregates
{
    public interface IAggregateRootFactory
    {
        TAggregateRoot Create<TAggregateRoot>()
            where TAggregateRoot : class, IAggregateRoot
            ;
    }
=======
namespace Ark.Tools.EventSourcing.Aggregates;

public interface IAggregateRootFactory
{
    TAggregateRoot Create<TAggregateRoot>()
        where TAggregateRoot : class, IAggregateRoot
        ;
>>>>>>> After
namespace Ark.Tools.EventSourcing.Aggregates;

public interface IAggregateRootFactory
{
    TAggregateRoot Create<TAggregateRoot>()
        where TAggregateRoot : class, IAggregateRoot
        ;
}