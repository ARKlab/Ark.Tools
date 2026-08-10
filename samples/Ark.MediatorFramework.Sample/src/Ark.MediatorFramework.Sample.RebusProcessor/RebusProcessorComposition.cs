// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Generated;
using Ark.Tools.Rebus;
using Ark.Tools.Rebus.Retry;
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
    /// <param name="secondLevelRetriesEnabled">
    /// Whether failed messages should be dispatched as <see cref="Rebus.Retry.Simple.IFailed{TMessage}"/>.
    /// </param>
    /// <param name="registerHandlers">Registers generated Rebus message handlers.</param>
    /// <param name="configureOptions">Configures optional Rebus processor options.</param>
    /// <param name="configureTimeouts">Configures optional Rebus timeout storage.</param>
    /// <returns>An isolated processor container.</returns>
    public static Container BuildContainer(
        InMemNetwork network,
        bool useSqlStore = true,
        string? connectionString = null,
        IClock? clock = null,
        ISampleDataContextFactory? dataContextFactory = null,
        IPrintCompletedNotificationService? printCompletedNotificationService = null,
        Action<Container>? registerHandlers = null,
        bool secondLevelRetriesEnabled = false,
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

        (registerHandlers ?? ArkGeneratedEndpoints.RegisterArkRebusHandlersFromAssembly<CompleteGreetingCompositionRequest>)(container);
        container.RegisterDecorator(typeof(IHandleMessages<>), typeof(RebusScopeDecorator<>));

        container.ConfigureRebus(cfg =>
        {
            cfg.Transport(transport =>
            {
                transport.UseInMemoryTransport(network, "ark.mediator.sample");
                ApplicationComposition.ConfigureRebusOutbox(transport, container, startProcessor: true);
            });
            ApplicationComposition.ConfigureRebusCommon(cfg, container, ArkGeneratedEndpoints.ConfigureArkRebusRouting<CompleteGreetingCompositionRequest>, options =>
            {
                options.SetNumberOfWorkers(1);
                options.ArkRetryStrategy(
                    maxDeliveryAttempts: 2,
                    secondLevelRetriesEnabled: secondLevelRetriesEnabled);
                configureOptions?.Invoke(options);
            });
            if (configureTimeouts is not null)
                cfg.Timeouts(configureTimeouts);
        });

        return container;
    }
}
