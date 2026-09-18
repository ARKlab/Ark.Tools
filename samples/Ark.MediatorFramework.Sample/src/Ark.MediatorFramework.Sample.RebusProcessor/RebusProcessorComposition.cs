// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;
using Ark.Tools.MediatorFramework.Rebus;
using Ark.Tools.Rebus;
using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;

using NodaTime;

using Rebus.Config;
using Rebus.Handlers;
using Rebus.Timeouts;
using Rebus.Transport.InMem;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.RebusProcessor;

/// <summary>Generated Rebus host for the sample background processor.</summary>
[ArkRebusHost(typeof(SampleMessagingParticipant))]
public sealed partial class RebusProcessorHost;

/// <summary>Builds the isolated full Rebus processor composition.</summary>
public static class RebusProcessorComposition
{
    /// <summary>Builds a container with Rebus receivers, generated message handlers, and the outbox processor.</summary>
    /// <param name="network">The shared in-memory transport network.</param>
    /// <param name="useSqlStore">Whether to use SQL persistence and the outbox.</param>
    /// <param name="connectionString">Optional SQL Server connection string.</param>
    /// <param name="clock">Optional clock override used by tests.</param>
    /// <param name="dataContextFactory">
    /// Optional context factory shared with the API container. When <see langword="null"/>
    /// and <paramref name="useSqlStore"/> is <see langword="false"/>, a new in-memory factory is created.
    /// </param>
    /// <param name="printCompletedNotificationService">Optional external print-completion notification service.</param>
    /// <param name="configureOptions">Configures optional Rebus processor options.</param>
    /// <param name="configureTimeouts">Configures optional Rebus timeout storage.</param>
    /// <returns>An isolated processor container.</returns>
    public static Container BuildContainer(
        InMemNetwork network,
        bool useSqlStore = true,
#if NET10_0_OR_GREATER
        [InfrastructureSecret] string? connectionString = null,
#else
        string? connectionString = null,
#endif
        IClock? clock = null,
        ISampleDataContextFactory? dataContextFactory = null,
        IPrintCompletedNotificationService? printCompletedNotificationService = null,
        Action<OptionsConfigurer>? configureOptions = null,
        Action<StandardConfigurer<ITimeoutManager>>? configureTimeouts = null)
    {
        ArgumentNullException.ThrowIfNull(network);

        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        ApplicationComposition.Register(
            container,
            useSqlStore,
            connectionString,
            clock,
            dataContextFactory,
            printCompletedNotificationService);
        container.RegisterAuthorization();
        container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();
        container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, RebusPrincipalContextWithFallbackProvider>();

        var requirements = RebusProcessorHost.GetRequirements();
        RebusProcessorHost.Register(
            (serviceType, implementationType) => container.Collection.Append(serviceType, implementationType));
        container.RegisterSingleton<Ark.Tools.MediatorFramework.Rebus.RebusMessagingBus>(() =>
            new Ark.Tools.MediatorFramework.Rebus.RebusMessagingBus(
                container.GetInstance<global::Rebus.Bus.IBus>(),
                requirements.Identity,
                requirements.PublishedEventTypes));
        container.RegisterSingleton<Ark.Tools.MediatorFramework.IBus>(
            () => container.GetInstance<Ark.Tools.MediatorFramework.Rebus.RebusMessagingBus>());
        container.RegisterSingleton<Ark.Tools.MediatorFramework.IBusOutboxEnlistment>(
            () => container.GetInstance<Ark.Tools.MediatorFramework.Rebus.RebusMessagingBus>());
        container.RegisterDecorator(typeof(IHandleMessages<>), typeof(RebusScopeDecorator<>));

        container.ConfigureRebus(cfg =>
        {
            cfg.Transport(transport =>
            {
                transport.UseInMemoryTransport(network, "ark-mediator-sample");
                ApplicationComposition.ConfigureRebusOutbox(transport, container, startProcessor: true);
            });
            ApplicationComposition.ConfigureRebusCommon(cfg, container, RebusProcessorHost.ConfigureRouting, options =>
            {
                options.SetNumberOfWorkers(1);
                RebusProcessorHost.ConfigureOptions(options);
                configureOptions?.Invoke(options);
            });
            if (configureTimeouts is not null)
                cfg.Timeouts(configureTimeouts);
        });

        return container;
    }
}
