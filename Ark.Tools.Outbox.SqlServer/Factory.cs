using Ark.Tools.Core;
using Ark.Tools.SimpleInjector;
using Ark.Tools.Sql;

using Autofac;

using Microsoft.ApplicationInsights.Extensibility.Implementation;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    internal class Factory
    {
        public Container Container { get; private set; }

        //public void RegisterInto(Container container)
        //{
        //    this.WithContainer(container);

        //    container.RegisterInstance(this);

        //    Container = container;
        //}

        public Factory(Container container)
        {
            Container = container;

            //Container.RegisterSingleton<Func<IsolationLevel, CancellationToken, ValueTask<ISqlContextAsync<IContextAsync>>>>(() => 
            //                    (new ServiceFactory1<IContextAsync>()).Create());

            Container.Register<IContextFactory<IRECDataContext>, RECDataContextFactory>();

            Container.RegisterSingleton<IContextFactory<IRECDataContext>>(
                    () => new RECDataContextFactory(container.GetInstance<IRECDataContextConfig>(), container.GetInstance<IDbConnectionManager>()));

            Container.RegisterSingleton<IContextFactory<IRECDataContext>, RECDataContextFactory>();


            Container.RegisterSingleton<IContextFactory2<IRECDataContext>, RECDataContextFactory2<IRECDataContext>>();
        }

        public void WithExternalContext(Func<IContextFactory2<IExternalContext>> externalContext = null)
        {
            if (externalContext == null)
                Container.RegisterSingleton<IContextFactory2<IExternalContext>, ExternalContextFactory<IExternalContext>>();
            else
            {
                //Container.RegisterSingleton<IContextFactory2<IExternalContext>, TestContextFactory<IExternalContext>>();
                Container.Register(externalContext);
                //Container.RegisterSingleton<IExternalContext, TestContext>();

            }

            //return this;
        }    //    public T FactoryGet<T>() where T : new () 
             //    {
             //        using (AsyncScopedLifestyle.BeginScope(Container))
             //        {
             //            var service = Container.GetInstance<typeof(T)>();
             //        }

        //    //        public ApiHost WithScopedContainer()
        //    //        {
        //    //#pragma warning disable CA2000 // Dispose objects before losing scope
        //    //            var container = new Container();
        //    //#pragma warning restore CA2000 // Dispose objects before losing scope
        //    //            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        //    //            return WithContainer(container);
        //    //        }

        //    //public void WithContainer(Container container)
        //    //{
        //    //    Container = container;

        //    //    Container.AllowResolvingFuncFactories();

        //    //    Container.RequireSingleton<ICommandProcessor, SimpleInjectorCommandProcessor>();
        //    //    Container.RequireSingleton<IQueryProcessor, SimpleInjectorQueryProcessor>();
        //    //    Container.RequireSingleton<IRequestProcessor, SimpleInjectorRequestProcessor>();

        //    //    //public void RegisterContainer()
        //    //    //{
        //    //    //    var builder = new ContainerBuilder();
        //    //    //    //builder.Register(context =>
        //    //    //    //{
        //    //    //    //    // make sure not to capture temporary context:
        //    //    //    //    // https://autofaccn.readthedocs.io/en/latest/advanced/concurrency.html#service-resolution
        //    //    //    //    var connectionString = context.Resolve<IConfiguration>().GetConnectionString("MyDb");

        //    //    //    //    return new Func<Task<IRECDataContext>>(async () =>
        //    //    //    //    {
        //    //    //    //        var connectionManager = new ReliableSqlConnectionManager();
        //    //    //    //        await connectionManager.GetAsync(config.SQLConnectionString);

        //    //    //    //        return connection;
        //    //    //    //    });
        //    //    //    //});

        //    //    //    builder.Register(async c =>
        //    //    //    {
        //    //    //        var bar = c.Resolve<IRECDataContext>();

        //    //    //        await bar.InitializeCtx();
        //    //    //        return bar;
        //    //    //    })
        //    //    //    .As<IRECDataContext>()
        //    //    //    .InstancePerDependency();

        //    //    //    var container = builder.Build();
        //    //    //}


        //    //}
        //}
    }
