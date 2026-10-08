// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Authorization;
using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Application.JsonContext;
using Ark.Tools.MediatorFramework;
using Ark.Tools.MediatorFramework.Rebus;
using Ark.Tools.Outbox;
using Ark.Tools.Rebus;
using Ark.Tools.Solid.Authorization;

using Rebus.Config;
using Rebus.DataBus;
using Rebus.DataBus.ClaimCheck;
using Rebus.Handlers;
using Rebus.Routing;
using Rebus.Serialization.Json;
using Rebus.Transport;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Core.WebRebus.Hosting;

/// <summary>Rebus composition shared by every process of the WebRebus variant.</summary>
public static class RebusHosting
{
    /// <summary>The Azure Blob Storage container of the Rebus claim-check DataBus.</summary>
    public const string DataBusContainerName = "rebus-databus";

    /// <summary>
    /// The body size above which Rebus sends a message as a DataBus claim check: 256 KB (the Service Bus
    /// message limit) minus 64 KB of headers and a 2 KB margin.
    /// </summary>
    public const int BigMessageThresholdBytes = (256 - 64 - 2) * 1024;

    /// <summary>Creates the application container for one process.</summary>
    /// <param name="options">The application options.</param>
    /// <returns>The populated, unverified container.</returns>
    public static Container CreateContainer(ApplicationOptions options)
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        ApplicationComposition.Register(container, options);
        container.RegisterAuthorization();
        container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();
        return container;
    }

    /// <summary>Configures Rebus for one WebRebus process.</summary>
    /// <typeparam name="THost">The generated Rebus host bound to the process participant.</typeparam>
    /// <param name="container">The application container.</param>
    /// <param name="transport">Selects the Rebus transport.</param>
    /// <param name="dataBusStorage">
    /// Selects the claim-check DataBus storage, shared by every process of the variant: Azure Blob Storage in
    /// production (<see cref="DataBusContainerName"/>), in-memory in tests.
    /// </param>
    /// <param name="startOutboxProcessor">Whether this process drains the Rebus outbox.</param>
    /// <param name="configureOptions">Optional extra Rebus options, used by tests.</param>
    /// <param name="configureTest">Optional extra Rebus configuration (in-memory subscriptions, timeouts), used by tests.</param>
    public static void Configure<THost>(
        Container container,
        Action<StandardConfigurer<ITransport>> transport,
        Action<StandardConfigurer<IDataBusStorage>> dataBusStorage,
        bool startOutboxProcessor,
        Action<OptionsConfigurer>? configureOptions = null,
        Action<RebusConfigurer>? configureTest = null)
        where THost : IArkRebusHost
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(dataBusStorage);
        var requirements = THost.GetRequirements();
        THost.Register((serviceType, implementationType) => container.Collection.Append(serviceType, implementationType));
        container.RegisterDecorator(typeof(IHandleMessages<>), typeof(RebusScopeDecorator<>));
        container.RegisterSingleton(() => new RebusMessagingBus(
            container.GetInstance<global::Rebus.Bus.IBus>(),
            requirements.Identity,
            requirements.PublishedEventTypes));
        container.RegisterSingleton<IBus>(() => container.GetInstance<RebusMessagingBus>());
        container.RegisterSingleton<IBusOutboxEnlistment>(() => container.GetInstance<RebusMessagingBus>());
        container.ConfigureRebus(cfg =>
        {
            cfg.Transport(t =>
            {
                transport(t);
                _configureOutbox(t, container, startOutboxProcessor);
            });
            cfg.DataBus(d =>
            {
                dataBusStorage(d);
                d.SendBigMessagesAsAttachments(BigMessageThresholdBytes);
            });
            _configureCommon(cfg, container, THost.ConfigureRouting, options =>
            {
                THost.ConfigureOptions(options);
                configureOptions?.Invoke(options);
            });
            configureTest?.Invoke(cfg);
        });
    }

    /// <summary>
    /// Configures the Rebus outbox. Every process enlists through it; only the process that sets
    /// <paramref name="startProcessor"/> drains it.
    /// </summary>
    private static void _configureOutbox(
        StandardConfigurer<ITransport> transport,
        Container container,
        bool startProcessor)
    {
        transport.Outbox(outbox =>
        {
            outbox.OutboxAsyncContextFactory(factory => factory.Use(container.GetInstance<IOutboxAsyncContextFactory>()));
            outbox.OutboxOptions(options => options.StartProcessor = startProcessor);
        });
    }

    /// <summary>Configures routing, logging, serialization, user-context flow, and telemetry.</summary>
    private static void _configureCommon(
        RebusConfigurer config,
        Container container,
        Action<StandardConfigurer<IRouter>> configureRouting,
        Action<OptionsConfigurer> configureOptions)
    {
        config.Routing(configureRouting);
        config.Logging(static logging => logging.NLog());
        config.Serialization(static serializer =>
        {
            var contextOptions = new JsonSerializerOptions
            {
                RespectNullableAnnotations = true,
                RespectRequiredConstructorParameters = true
            }.ConfigureArkDefaults();
            var jsonContext = new ApplicationJsonSerializerContext(contextOptions);
            var rebusOptions = new JsonSerializerOptions
            {
                RespectNullableAnnotations = true,
                RespectRequiredConstructorParameters = true
            }.ConfigureArkDefaults();
            rebusOptions.TypeInfoResolver = jsonContext;
            serializer.UseSystemTextJson(rebusOptions);
        });
        config.Options(options =>
        {
            options.AutomaticallyFlowUserContext(container);
            options.UseOpenTelemetry(container);
            options.UseOpenTelemetryMetrics(container);
            configureOptions(options);
        });
    }
}
