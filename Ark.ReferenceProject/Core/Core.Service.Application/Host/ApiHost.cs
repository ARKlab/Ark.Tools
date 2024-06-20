using Ark.Tools.Authorization.Requirement;
using Ark.Tools.NewtonsoftJson;
using Ark.Tools.Outbox;
using Ark.Tools.Rebus;
using Ark.Tools.Rebus.AzureServiceBus;
using Ark.Tools.Rebus.Retry;
using Ark.Tools.Rebus.Tests;
using Ark.Tools.SimpleInjector;
using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;
using Ark.Tools.Solid.SimpleInjector;
using Ark.Tools.Sql;
using Ark.Tools.Sql.SqlServer;

using Azure.Identity;
using Core.Service.Application.Config;
using Core.Service.Application.DAL;
using Core.Service.Application.Handlers;
using Core.Service.Common.Auth;

using Ark.Reference.Common;
using Ark.Reference.Common.Auth;
using Ark.Reference.Common.Services.Auth;
using Ark.Reference.Common.Services.Decorators;
using Ark.Reference.Common.Services.FileStorageService;

using FluentValidation;

using Newtonsoft.Json;

using NodaTime;

using Rebus.Compression;
using Rebus.Config;
using Rebus.DataBus.ClaimCheck;
using Rebus.Handlers;
using Rebus.Persistence.InMem;
using Rebus.Retry.FailFast;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using Rebus.Time;
using Rebus.Transport.InMem;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using Core.Service.API.Messages;

namespace Core.Service.Application.Host
{
    public class ApiHost
    {
        public ApiHost(IApiHostConfig config)
        {
            this.Config = config;
            this._applicationAssemblies = new Assembly[] {
                  typeof(ApiHost).Assembly
            };
        }

        public void RegisterInto(Container container)
        {
            this.WithContainer(container);

            container.RegisterInstance(this);

            Container = container;
        }

        public ApiHost WithScopedContainer()
        {
            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            return WithContainer(container);
        }

        public ApiHost WithContainer(Container container)
        {
            Container = container;

            Container.AllowResolvingFuncFactories();

            Container.RequireSingleton<ICommandProcessor, SimpleInjectorCommandProcessor>();
            Container.RequireSingleton<IQueryProcessor, SimpleInjectorQueryProcessor>();
            Container.RequireSingleton<IRequestProcessor, SimpleInjectorRequestProcessor>();
            Container.RequireSingleton<IDbConnectionManager, ReliableSqlConnectionManager>();

#if DEBUG
            if (Debugger.IsAttached)
                Container.RegisterDecorator<IDbConnectionManager, SqlConnectionManagerLeakDecorator>(Lifestyle.Singleton);
#endif
            _registerContainer(Container);

            Container.RegisterInstance(this);


            return this;
        }


        public ApiHost WithAuthorization()
        {
            Container.RegisterAuthorizationBase();
            Container.RegisterDecorator(typeof(IQueryHandler<,>), typeof(PolicyAuthorizeOrLogicQueryDecorator<,>));
            Container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(PolicyAuthorizeOrLogicRequestDecorator<,>));
            Container.RegisterDecorator(typeof(ICommandHandler<>), typeof(PolicyAuthorizeOrLogicCommandDecorator<>));


            Container.RegisterAuthorizationHandler<CoreRequiredScopePolicyHandler>();

            Container.RegisterAuthorizationHandler<Ark.Reference.Common.Auth.PermissionAuthorizationHandler<Permissions>>();
            Container.Register<IUserPermissionsProvider<Permissions>, PermissionsProvider>();

            return this;
        }

        public ApiHost WithIClock(IClock clock = null)
        {
            if (clock == null)
            {
                Container.RegisterSingleton<IClock>(() => SystemClock.Instance);
            }
            else
                Container.RegisterInstance(clock);

            return this;
        }

        public ApiHost WithRebus(Queue queue = Queue.Main, InMemNetwork inMemNetwork = null, InMemorySubscriberStore inMemorySubscriberStore = null)
        {
            if (queue != Queue.OneWay)
            {
                var handlers = Container.GetTypesToRegister((queue switch
                {
                    Queue.Main => typeof(IHandleMessagesCore<>),
                    _ => throw new NotSupportedException()
                }), this._applicationAssemblies);
                Container.Collection.Register(typeof(IHandleMessages<>), handlers);

                Container.RegisterDecorator(typeof(IHandleMessages<>), typeof(RebusScopeDecorator<>));
                Container.RegisterDecorator(typeof(IHandleMessages<>), typeof(RebusLogDecorator<>));
            }

            Container.ConfigureRebus(_ => _
                .Logging(l => l.NLog())
                .Transport(t =>
                {
                    if (queue == Queue.OneWay)
                    {
                        if (inMemNetwork == null)
                        {
                            if (this.Config.AsbConnectionString.Contains("SharedAccess"))
                                t.UseAzureServiceBusAsOneWayClient(this.Config.AsbConnectionString);
                            else
                                t.UseAzureServiceBusAsOneWayClient(this.Config.AsbConnectionString, new DefaultAzureCredential());
                        }
                        else
                            t.UseDrainableInMemoryTransportAsOneWayClient(inMemNetwork);
                    }
                    else
                    {
                        var listeningQueue = this.Config.RequestQueue + queue switch
                        {
                            Queue.Main => "",
                            _ => throw new NotSupportedException()
                        };
                        if (inMemNetwork == null)
                        {
                            if (this.Config.AsbConnectionString.Contains("SharedAccess"))
                                t.UseAzureServiceBus(this.Config.AsbConnectionString, listeningQueue)
                                      .EnablePartitioning()
                                      .AutomaticallyRenewPeekLock()
                                      .UseNativeMessageDeliveryCount()
                                      ;
                            else
                                t.UseAzureServiceBus(this.Config.AsbConnectionString, listeningQueue, new DefaultAzureCredential())
                                    .EnablePartitioning()
                                    .AutomaticallyRenewPeekLock()
                                    .UseNativeMessageDeliveryCount()
                                    ;

                            t.UseNativeDeadlettering();     
                        }
                        else
                        {
                            t.UseDrainableInMemoryTransport(inMemNetwork, listeningQueue);
                            //t.UseFakeDeliveryCount(3); // =maxDeliveryAttempts
                        }
                    }

                    t.Outbox(o =>
                    {
                        // this is used only by the Outbox processor, not on Send() or Publish()
                        o.OutboxContextFactory(c => c.Use(Container.GetInstance<Func<IOutboxContext>>()));
                        o.OutboxOptions(o => o.StartProcessor = queue == Queue.Main);
                        o.OutboxOptions(o => o.MaxMessagesPerBatch = 10);
                    });
                })
                .Subscriptions(s =>
                {
                    if (inMemorySubscriberStore != null)
                        s.StoreInMemory(inMemorySubscriberStore);
                })
                .Routing(r =>
                {
                    r.TypeBased()
                        .MapAssemblyNamespaceOf<Ping_ProcessMessage.V1>(this.Config.RequestQueue);
                    ;
                    //r.ForwardOnException<Exception>("error", LogLevel.Error, ex => _isContractException(ex));
                })
                .Options(o =>
                {
                    o.ArkRetryStrategy(errorDetailsHeaderMaxLength: 10000, secondLevelRetriesEnabled: true, maxDeliveryAttempts: 3);

                    if (inMemNetwork != null)
                    {
                        o.SetMaxParallelism(2);
                    }
                    else
                    {
                        o.SetMaxParallelism(Environment.ProcessorCount * 4);
                    }

                    o.EnableCompression();
                    o.AutomaticallyFlowUserContext(Container);
                    o.UseApplicationInsight(Container);
                    o.UseApplicationInsightMetrics(Container);
                    o.FailFastOn<Exception>(ex => ex.IsFinal());
                    if (inMemNetwork != null)
                        o.AddInProcessMessageInspector();
                })
                .DataBus(d =>
                {
                    d.UseCompression(DataCompressionMode.Always)
                        .StoreInBlobStorage(this.Config.StorageConnectionString, CommonConstants.RebusDataBusContainerName)
                        .DoNotUpdateLastReadTime()
                        ;
                    d.SendBigMessagesAsAttachments((256 - 64 - 2) * 1024); // 256KB (max size) - 64KB (max headers) - 2KB (just in case)

                })
                .Serialization(s =>
                {
                    var cfg = new ArkDefaultJsonSerializerSettings();
                    cfg.TypeNameHandling = TypeNameHandling.Auto;
                    cfg.ObjectCreationHandling = ObjectCreationHandling.Replace;
                    s.UseNewtonsoftJson(cfg);
                })
                .Timeouts(t =>
                {
                    t.OtherService<IRebusTime>().Register(c => new RebusIClock(Container.GetInstance<IClock>()));
                    if (inMemNetwork != null)
                        t.StoreInMemoryTests();
                })
            );

            return this;
        }

        public async void RunBusInBackground()
        {
            Container.StartBus();

            await this.Container.GetInstance<IFileStorageService>().InitAsync();
        }


        private void _registerContainer(Container container)
        {
            //Cfg
            container.RegisterInstance(this.Config);
            container.RegisterInstance<IRebusBusConfig>(this.Config);
            container.RegisterInstance<ICoreDataContextConfig>(this.Config);
            container.RegisterInstance<ICoreConfig>(this.Config);
            container.RegisterInstance<IFileStorageServiceConfig>(this.Config);

            var validatorAndHandlerAssemblies = new Assembly[] {
                    typeof(ApiHost).Assembly
                  , typeof(CommonConstants).Assembly
            };

            container.Register(typeof(IValidator<>),
                container.GetTypesToRegister(typeof(IValidator<>), validatorAndHandlerAssemblies)
                    .Where(x => x.IsPublic)
                , Lifestyle.Singleton);

            container.Register(typeof(IQueryHandler<,>), validatorAndHandlerAssemblies);
            container.Register(typeof(IRequestHandler<,>), validatorAndHandlerAssemblies);
            container.Register(typeof(ICommandHandler<>), validatorAndHandlerAssemblies);
            container.RegisterConditional(typeof(IValidator<>), typeof(NullValidator<>), Lifestyle.Singleton,
                c => !c.Handled);

            container.RegisterDecorator(
                     typeof(IRequestHandler<,>)
                   , typeof(OptimisticConcurrencyRetrierDecorator<,>)
                );

            container.RegisterDecorator(
                     typeof(IQueryHandler<,>)
                   , typeof(QueryFluentValidateDecorator<,>)
                );

            container.RegisterDecorator(
                     typeof(IRequestHandler<,>)
                   , typeof(RequestFluentValidateDecorator<,>)
                );

            //Dal
            container.RegisterSingleton<CoreDataContextFactory>();

            var rw = Lifestyle.Singleton.CreateRegistration<Func<ICoreDataContext>>(() => () => container.GetInstance<CoreDataContextFactory>().Get(), container);
            var ro = Lifestyle.Singleton.CreateRegistration<Func<ICoreDataContext>>(() => () => container.GetInstance<CoreDataContextFactory>().Get(), container);

            var isIQuery = (PredicateContext ctx) => ctx.HasConsumer && ctx.Consumer.ImplementationType.IsClosedTypeOf(typeof(IQueryHandler<,>));
            container.RegisterConditional<Func<ICoreDataContext>>(rw, ctx => !isIQuery(ctx));
            container.RegisterConditional<Func<ICoreDataContext>>(ro, ctx => isIQuery(ctx));

            container.RegisterInstance<Func<IOutboxContext>>(() => container.GetInstance<CoreDataContextFactory>().Get());

            //Service
            container.Register<IFileStorageService, FileStorageService>();
        }

        public ApiHost WithServiceIdentity(Func<ClaimsPrincipal> getter)
        {
            Container.RegisterSingleton<IContextProvider<ClaimsPrincipal>>(
                () => new ExternalPrincipalContextProvider(getter));
            return this;
        }

        public ApiHost WithServiceIdentity(ClaimsPrincipal claimsPrincipal)
        {
            Container.RegisterSingleton<IContextProvider<ClaimsPrincipal>>(
                () => new ExternalPrincipalContextProvider(() => claimsPrincipal));
            return this;
        }

        public ApiHost WithRebusIdentity()
        {
            Container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, RebusPrincipalContextProvider>();
            return this;
        }

        public Container Container { get; private set; }
        public IApiHostConfig Config { get; private set; }

        private readonly Assembly[] _applicationAssemblies;

        private class NullValidator<T> : AbstractValidator<T>
        {
        }

    }
    class ExternalPrincipalContextProvider : IContextProvider<ClaimsPrincipal>
    {
        private readonly Func<ClaimsPrincipal> _getter;

        public ExternalPrincipalContextProvider(Func<ClaimsPrincipal> getter)
        {
            _getter = getter;
        }

        public ClaimsPrincipal Current => _getter();
    }

    class RebusPrincipalContextProvider : IContextProvider<ClaimsPrincipal>
    {
        private readonly IMessageContextProvider _messageContextProvider;

        public RebusPrincipalContextProvider(IMessageContextProvider messageContextProvider)
        {
            _messageContextProvider = messageContextProvider;
        }

        public ClaimsPrincipal Current => _messageContextProvider.Current?.IncomingStepContext.Load<ClaimsPrincipal>();
    }

    public enum Queue
    {
        OneWay,
        Main,
    }

    class CoreRequiredScopePolicyHandler : RequiredScopePolicyHandler
    {
        public CoreRequiredScopePolicyHandler() : base(AuthConstants.ScopePrefix)
        {
        }
    }
}
