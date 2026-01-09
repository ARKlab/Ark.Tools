using Ark.Tools.EventSourcing.Aggregates;
using Ark.Tools.EventSourcing.Store;
using Ark.Tools.RavenDb.Auditing;
using Ark.Tools.Solid;

using Raven.Client.Documents;
using Raven.Client.Documents.Session;

using SimpleInjector;

using System.Security.Claims;

namespace Ark.Tools.EventSourcing.RavenDb;

public class RavenDbEventSourcingAggregateTransactionFactory<TAggregateRoot, TAggregateState, TAggregate>
    : IAggregateTransactionFactory<TAggregateRoot, TAggregateState, TAggregate>
    where TAggregateRoot : AggregateRoot<TAggregateRoot, TAggregateState, TAggregate>
    where TAggregateState : AggregateState<TAggregateState, TAggregate>, new()
    where TAggregate : IAggregate
{
    private readonly IRavenDbSessionFactory _sessionFactory;
    private readonly IAggregateRootFactory _aggregateRootFactory;

    public RavenDbEventSourcingAggregateTransactionFactory(
        IRavenDbSessionFactory sessionFactory,
        IAggregateRootFactory aggregateRootFactory
        )
    {
        _sessionFactory = sessionFactory;
        _aggregateRootFactory = aggregateRootFactory;
    }

    public async Task<IAggregateTransaction<TAggregateRoot, TAggregateState, TAggregate>> StartTransactionAsync(string id, CancellationToken ctk = default)
    {
        var session = _sessionFactory.Create(new SessionOptions
        {
            NoTracking = false,
            TransactionMode = TransactionMode.ClusterWide
        });

        var tx = new RavenDbEventSourcingAggregateTransaction<TAggregateRoot, TAggregateState, TAggregate>(session, id, _aggregateRootFactory);

        try
        {
            await tx.LoadAsync(ctk).ConfigureAwait(false);
        }
        catch
        {
            tx.Dispose();
            throw;
        }
        return tx;
    }


    public async Task<TAggregateState> LoadCapturedState(string id, CancellationToken ctk = default)
    {
        using var session = _sessionFactory.Create(new SessionOptions
        {
            //NoTracking = true,
            TransactionMode = TransactionMode.SingleNode
        });
        return await session.LoadAsync<TAggregateState>(AggregateHelper<TAggregate>.Name + "/" + id, ctk).ConfigureAwait(false);
    }
}

public interface IRavenDbSessionFactory
{
    IAsyncDocumentSession Create(SessionOptions options);
}

public class SimpleInjectorAuditedSessionFactory : IRavenDbSessionFactory
{
    private readonly Container _container;

    public SimpleInjectorAuditedSessionFactory(Container container)
    {
        _container = container;
    }

    public virtual IAsyncDocumentSession Create(SessionOptions options)
    {
        var store = _container.GetInstance<IDocumentStore>();
        var principalProvider = _container.GetInstance<IContextProvider<ClaimsPrincipal>>();
        var session = store.OpenAsyncSession(options);

        return new AuditableAsyncDocumentSessionDecorator(session, principalProvider);
    }
}