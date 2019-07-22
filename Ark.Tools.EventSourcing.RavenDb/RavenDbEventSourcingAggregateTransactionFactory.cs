using Ark.Tools.EventSourcing.Aggregates;
using Ark.Tools.EventSourcing.Store;
using Ark.Tools.RavenDb.Auditing;
using Ark.Tools.Solid;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using SimpleInjector;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.EventSourcing.RavenDb
{
    public class RavenDbEventSourcingAggregateTransactionFactory<TAggregate, TAggregateState>
        : IAggregateTransactionFactory<TAggregate, TAggregateState>
        where TAggregate : AggregateRoot<TAggregate, TAggregateState>, new()
        where TAggregateState : AggregateState<TAggregate, TAggregateState>, new()
    {
		private readonly IRavenDbSessionFactory _sessionFactory;

		public RavenDbEventSourcingAggregateTransactionFactory(IRavenDbSessionFactory sessionFactory)
        {
			_sessionFactory = sessionFactory;
        }

        public async Task<IAggregateTransaction<TAggregate, TAggregateState>> StartTransactionAsync(string id, CancellationToken ctk = default)
        {
			var session = _sessionFactory.Create(new SessionOptions
			{
				NoTracking = false,
				TransactionMode = TransactionMode.ClusterWide
			});

			var tx = new RavenDbEventSourcingAggregateTransaction<TAggregate, TAggregateState>(session, id);

            try
            {
                await tx.LoadAsync(ctk);
            } catch
            {
                tx.Dispose();
                throw;
            }
            return tx;
        }


		public async Task<TAggregateState> LoadCapturedState(string id, CancellationToken ctk = default)
		{
			using (var session = _sessionFactory.Create(new SessionOptions
			{
				//NoTracking = true,
				TransactionMode = TransactionMode.SingleNode
			}))
			{
				return await session.LoadAsync<TAggregateState>(AggregateHelper<TAggregate>.Name + "/" + id, ctk);
			}
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
}
