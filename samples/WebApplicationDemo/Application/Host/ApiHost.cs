using Ark.Tools.Http;
using Ark.Tools.SimpleInjector;
using Ark.Tools.Solid;
using Ark.Tools.Solid.SimpleInjector;
using Ark.Tools.Sql;
using Ark.Tools.Sql.SqlServer;

using FluentValidation;

using SimpleInjector;

using System.Linq;
using System.Reflection;
using System.Threading;

using WebApplicationDemo.Dto;
using WebApplicationDemo.Services;

namespace WebApplicationDemo.Application.Host
{
    public class ApiHost
    {
        public ApiHost(ApiConfig config)
        {
            this.Config = config;

            this._applicationAssemblies = [
                typeof(ApiHost).Assembly,
                //Assembly.Load("ProblemDetailsSample")
            ];
        }

        public ApiHost WithContainer(Container container)
        {
            Container = container;

            Container.AllowResolvingFuncFactories();

            Container.RequireSingleton<ICommandProcessor, SimpleInjectorCommandProcessor>();
            Container.RequireSingleton<IQueryProcessor, SimpleInjectorQueryProcessor>();
            Container.RequireSingleton<IRequestProcessor, SimpleInjectorRequestProcessor>();
            Container.RequireSingleton<IDbConnectionManager, ReliableSqlConnectionManager>();

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

            container.Register<IPostService, PostService>(Lifestyle.Scoped);

            container.RegisterInstance<IArkFlurlClientFactory>(new ArkFlurlClientFactory());
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

        private sealed class NullValidator<T> : AbstractValidator<T>
        {
        }
    }
}