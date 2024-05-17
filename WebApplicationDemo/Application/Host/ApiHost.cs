using Ark.Tools.Solid;
using Ark.Tools.Sql;
using FluentValidation;
using SimpleInjector;
using System.Linq;
using System.Reflection;
using System.Threading;
using Ark.Tools.SimpleInjector;
using Ark.Tools.Solid.SimpleInjector;
using Ark.Tools.Sql.SqlServer;
using WebApplicationDemo.Dto;
using Ark.Tools.Core;
using Ark.Tools.Outbox.SqlServer;
using WebApplicationDemo.Configuration;
using System;

namespace WebApplicationDemo.Application.Host
{
	public class ApiHost
	{
		public ApiHost(ApiConfig config)
		{
			this.Config = config;

			this._applicationAssemblies = new Assembly[] {
				typeof(ApiHost).Assembly,
                //Assembly.Load("ProblemDetailsSample")
            };
		}

		public ApiHost WithContainer(Container container)
		{
			Container = container;

			Container.AllowResolvingFuncFactories();

			Container.RequireSingleton<ICommandProcessor, SimpleInjectorCommandProcessor>();
			Container.RequireSingleton<IQueryProcessor, SimpleInjectorQueryProcessor>();
			Container.RequireSingleton<IRequestProcessor, SimpleInjectorRequestProcessor>();
			Container.RequireSingleton<IDbConnectionManager, ReliableSqlConnectionManager>();
			
            Container.RequireSingleton<IContextFactory<ISqlDataContext>, TestContextAsyncFactory>();
            //Container.RegisterSingleton<TestContextAsyncFactory>();
            //container.RegisterSingleton<Func<SqlContextAsyncFactory<ISqlDataContext>>>();
            //Container.RequireSingleton<IContextFactory<DataContextSql>, TestContextAsyncFactory>();
            //Container.RegisterSingleton<SqlContextAsyncFactory<ISqlDataContext>, TestContextAsyncFactory>();
            //Container.RegisterSingleton<typeof(SqlContextAsyncFactory<ISqlDataContext>), typeof(TestContextAsyncFactory)>();
            //Container.AddRegistration(typeof(SqlContextAsyncFactory<ISqlDataContext>), typeof(TestContextAsyncFactory));

            Container.RegisterSingleton<ISqlDataContextConfig, SqlDataContextConfig>();
            Container.RegisterSingleton<IDataContextConfig, DataContextConfig>();

			_registerContainer(Container);

			return this;
		}

		private void _registerContainer(Container container)
		{
			container.Register(typeof(IValidator<>),
				container.GetTypesToRegister(typeof(IValidator<>), this._applicationAssemblies)
					.Where(x => x.IsPublic)
				, Lifestyle.Singleton);

			container.Register(typeof(IQueryHandler<,>), this._applicationAssemblies);
			container.Register(typeof(IRequestHandler<,>), this._applicationAssemblies);

			container.RegisterConditional(typeof(IValidator<>), typeof(NullValidator<>), Lifestyle.Singleton,
				c => !c.Handled);

			container.RegisterDecorator(
					 typeof(IQueryHandler<,>)
				   , typeof(QueryFluentValidateDecorator<,>)
				);

			container.RegisterDecorator(
					 typeof(IRequestHandler<,>)
				   , typeof(RequestFluentValidateDecorator<,>)
				);

			container.RegisterInstance(this.Config);

			//HealthChecks
			container.Register<ExampleHealthCheck>();
			container.Register<IExampleHealthCheckService, ExampleHealthCheckService>();

			// DAL
			//container.Register<ISqlContext<DataSql>, MiddlewareDataContext_Sql>();
		}

		public void RunInBackground()
		{

		}

		public void RunAndBlock()
		{
			this.RunInBackground();
			Thread.Sleep(Timeout.Infinite);
		}

		public Container? Container { get; private set; }

		public ApiConfig Config { get; private set; }

		private readonly Assembly[] _applicationAssemblies;

		private class NullValidator<T> : AbstractValidator<T>
		{
		}
	}
}
