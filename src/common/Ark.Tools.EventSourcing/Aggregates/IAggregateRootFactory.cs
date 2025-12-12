namespace Ark.Tools.EventSourcing.Aggregates
{
    public interface IAggregateRootFactory
    {
        TAggregateRoot Create<TAggregateRoot>()
            where TAggregateRoot : class, IAggregateRoot
            ;
    }
}