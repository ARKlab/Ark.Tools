using SimpleInjector;
using System.Reflection;
using System.Threading;
using Ark.Tools.SimpleInjector;
using Ark.Tools.RavenDb.Auditing;
using Raven.Client.Documents.Session;
using Raven.Client.Documents;
using Ark.Tools.EventSourcing.RavenDb;
using RavenDbSample.Models;
using Ark.Tools.EventSourcing.DomainEventPublisher;
using Ark.Tools.EventSourcing.Store;
using Ark.Tools.DomainEventPublisher.Rebus;
using Ark.Tools.Solid;
using RavenDbSample.Application.DAL;
using Ark.Tools.Solid.SimpleInjector;

namespace RavenDbSample.Application.Host
{
	public class ApiHost
    {
        public ApiHost(ApiConfig config)
        {
            this.Config = config;

            this._applicationAssemblies = new Assembly[] {
                typeof(ApiHost).Assembly,
            };
        }

        public ApiHost WithContainer(Container container)
        {
            Container = container;
			
			Container.AllowResolvingFuncFactories();

			Container.RequireSingleton<ICommandProcessor, SimpleInjectorCommandProcessor>();
			Container.RequireSingleton<IQueryProcessor, SimpleInjectorQueryProcessor>();
			Container.RequireSingleton<IRequestProcessor, SimpleInjectorRequestProcessor>();

			_registerContainer(Container);

            return this;
        }

		public ApiHost WithRavenDbAudit()
		{
			//Container.Register<IAsyncDocumentSession>(() => Container.GetInstance<IDocumentStore>().OpenAsyncSession(), Lifestyle.Scoped);

			Container.RegisterConditional<IAsyncDocumentSession>(
				Lifestyle.Scoped.CreateRegistration(
					() => Container.GetInstance<IDocumentStore>().OpenAsyncSession(), Container
					),
				ctx => !typeof(IDbContextClusterWide).IsAssignableFrom(ctx.Consumer.ImplementationType)
			);

			Container.RegisterConditional<IAsyncDocumentSession>(
				Lifestyle.Scoped.CreateRegistration(
					() => Container.GetInstance<IDocumentStore>().OpenAsyncClusterWideSession(), Container
					),
				ctx => typeof(IDbContextClusterWide).IsAssignableFrom(ctx.Consumer.ImplementationType)
			);

			Container.RegisterRavenDbAudit();
			return this;
		}

		private void _registerContainer(Container container)
        {
			container.RegisterInstance(this.Config);

			//EventSourcing
			container.RegisterSingleton<IDomainEventPublisher, RebusDomainEventPublisher>();
			container.RegisterSingleton<RavenDbDomainEventPublisher>();
			container.RegisterSingleton<IRavenDbSessionFactory, SimpleInjectorAuditedSessionFactory>();
			container.RegisterSingleton<
				IAggregateTransactionFactory<MyEntityAggregate, MyEntityState>, 
				RavenDbEventSourcingAggregateTransactionFactory<MyEntityAggregate, MyEntityState>>();

			// DAL
			container.Register<IDbContext, DbContext>();
			container.Register<IDbContextClusterWide, DbContextClusterWide>();

			container.Register(typeof(IQueryHandler<,>), this._applicationAssemblies);
			container.Register(typeof(IRequestHandler<,>), this._applicationAssemblies);

		}

		public void RunInBackground()
        {

        }

        public void RunAndBlock()
        {
            this.RunInBackground();
            Thread.Sleep(Timeout.Infinite);
        }

        public Container Container { get; private set; }

        public ApiConfig Config { get; private set; }

        private readonly Assembly[] _applicationAssemblies;
    }
}
