// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Authorization;
using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Application.JsonContext;
using Ark.MediatorFramework.Sample.Core.Application.Messages;
using Ark.Tools.AspNetCore.HealthChecks.OTel;
using Ark.Tools.MediatorFramework.AzureFunctions;
using Ark.Tools.MediatorFramework.AzureFunctions.SimpleInjector;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.MediatorFramework.Messaging.OTel;
using Ark.Tools.OTel;
using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;
using Ark.Tools.Solid.SimpleInjector;

using Azure.Monitor.OpenTelemetry.Exporter;

using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Security.Claims;
using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Core.Functions.Hosting;

/// <summary>Composition shared by every app of the Functions variant.</summary>
public static class FunctionsHosting
{
    /// <summary>
    /// Adds OpenTelemetry for a Functions worker: the Ark instrumentation, the messaging metrics and the Functions
    /// worker defaults. Azure Monitor receives traces and metrics only when a connection string is configured.
    /// </summary>
    /// <remarks>
    /// Neither the Azure Monitor distro nor <c>AddArkAspNetCoreOpenTelemetry</c> is used: their ASP.NET Core
    /// instrumentation would report every request the Functions host already reports. Logs are not exported by the worker either: with <c>telemetryMode: OpenTelemetry</c> the
    /// host captures the NLog console output and exports it, so a worker log exporter would send each entry twice.
    /// </remarks>
    /// <param name="services">The worker services.</param>
    /// <param name="configuration">The worker configuration.</param>
    public static void AddTelemetry(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddArkOTelHealthCheckPublisher();
        var telemetry = services.AddOpenTelemetry()
            .AddArkOpenTelemetry(configuration: configuration)
            .WithMetrics(static metrics => metrics
                .AddArkMessagingInstrumentation()
                .AddMeter(ArkOTelHealthCheckPublisher.MeterName));
        // Same lookup as AddArkAzureMonitorOpenTelemetry.
        var connectionString = configuration["ApplicationInsights:ConnectionString"]
            ?? configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            telemetry
                .WithTracing(tracing => tracing.AddAzureMonitorTraceExporter(o => o.ConnectionString = connectionString))
                .WithMetrics(metrics => metrics.AddAzureMonitorMetricExporter(o => o.ConnectionString = connectionString));
        }

        telemetry.UseFunctionsWorkerDefaults();
    }

    /// <summary>Creates the application container for one app.</summary>
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

    /// <summary>Gets the claim-check DataBus options shared by every Functions app.</summary>
    /// <remarks>
    /// Each app is a separate process that must read the attachments the others write, so the store
    /// is Azure Blob Storage, never the process-local in-memory DataBus. Locally the connection points
    /// at Azurite from docker-compose.
    /// </remarks>
    /// <param name="configuration">The app configuration.</param>
    /// <returns>The shared DataBus options.</returns>
    public static AzureBlobDataBusOptions DataBusOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new AzureBlobDataBusOptions
        {
            ContainerName = "amf1-databus",
            Prefix = "sample/",
            MinimumAttachmentLifetime = TimeSpan.FromDays(7),
            EnsureContainer = true,
            ConnectionString = configuration.GetConnectionString("DataBus")
                ?? throw new InvalidOperationException("ConnectionStrings:DataBus is required."),
        };
    }

    /// <summary>Registers message principal flow for a Functions app.</summary>
    /// <param name="services">The Functions service collection.</param>
    /// <param name="container">The application container.</param>
    /// <returns>The registered provider.</returns>
    public static MessagingPrincipalContextProvider AddUserContext(IServiceCollection services, Container container)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);
        var principal = new MessagingPrincipalContextProvider();
        services.AddSingleton<IContextProvider<ClaimsPrincipal>>(principal);
        services.AddSingleton(new UserContextIncomingStep(principal.Set));
        services.AddSingleton(new UserContextOutgoingStep(() => principal.Current));
        return principal;
    }

    /// <summary>Composes a trigger app: its generated Service Bus trigger, its outbox choice, and container bridge.</summary>
    /// <remarks>
    /// The order matters: the user context goes before <c>AddArkAzureFunctions</c> so its
    /// <c>TryAdd</c> keeps the message principal, and the processors are bridged before
    /// <c>ConfigureArkMessagingFunctions</c> validates the generated handlers against the container.
    /// </remarks>
    /// <param name="services">The Functions service collection.</param>
    /// <param name="configuration">The app configuration.</param>
    /// <param name="manifest">The app's generated messaging manifest.</param>
    /// <param name="container">The application container.</param>
    /// <param name="dataBus">
    /// Selects the claim-check DataBus: Azure Blob Storage from <see cref="DataBusOptions"/> in production, shared
    /// with the other apps; an in-memory instance shared by every process in tests.
    /// </param>
    /// <param name="outbox">
    /// Selects the outbox of an app whose handlers open an outbox scope, or <see langword="null"/> for an app whose
    /// handlers never send.
    /// </param>
    public static void AddMessagingTrigger(
        IServiceCollection services,
        IConfiguration configuration,
        MessagingFunctionsManifest manifest,
        Container container,
        Action<MessagingFunctionsDataBusBuilder> dataBus,
        Action<MessagingFunctionsOutboxBuilder>? outbox = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(dataBus);
        _configureMessagingJson(services);
        AddUserContext(services, container);
        services.AddArkAzureFunctions();
        services.AddArkSolidProcessors(container);
        services.ConfigureArkMessagingFunctions(
            configuration,
            manifest,
            messaging =>
            {
                messaging
                    .UseTransport(static transport => transport.UseServiceBus())
                    .UseDataBus(dataBus);
                if (outbox is not null)
                    messaging.UseOutbox(outbox);
            });
        services.AddArkAzureFunctionsSimpleInjectorBridge(container);
    }

    /// <summary>
    /// Composes the Api app's producer: it receives nothing and sends through the development outbox pass-through.
    /// </summary>
    /// <remarks>
    /// The outgoing user is the HTTP user that <c>AddArkAzureFunctions</c> registers, unless the
    /// caller registered another <see cref="IContextProvider{TItem}"/> of <see cref="ClaimsPrincipal"/> first.
    /// Nothing drains an outbox in a Functions-only deployment, so the handlers' outbox scope is the pass-through,
    /// which is not atomic and starts only in the Development environment.
    /// </remarks>
    /// <param name="services">The Functions service collection.</param>
    /// <param name="container">The application container.</param>
    /// <param name="transport">Selects the transport.</param>
    /// <param name="dataBus">Selects the claim-check DataBus.</param>
    public static void AddApiProducer(
        IServiceCollection services,
        Container container,
        Action<MessagingTransportBuilder> transport,
        Action<MessagingDataBusBuilder> dataBus)
    {
        _configureMessagingJson(services);
        services.AddArkAzureFunctions();
        services.AddArkSolidProcessors(container);
        services.AddSingleton(static serviceProvider => new UserContextOutgoingStep(
            () => serviceProvider.GetRequiredService<IContextProvider<ClaimsPrincipal>>().Current));
        services.ConfigureArkMessaging(
            SampleMessagingNetwork.CreateOptions(),
            SampleMessagingNetwork.Registry,
            messaging => messaging.Producer<SampleMessagingApiParticipant>(producer => producer
                .UseTransport(transport)
                .UseDataBus(dataBus)
                .UseOutgoingPipeline(typeof(UserContextOutgoingStep))
                .UseOutbox(static outbox => outbox.UseDevelopmentPassthrough())));
        services.AddArkAzureFunctionsSimpleInjectorBridge(container);
    }

    private static void _configureMessagingJson(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Configure<JsonSerializerOptions>(static options =>
        {
            // The Ark defaults (camelCase, EvolvableEnum and NodaTime converters) match the application contracts.
            options.ConfigureArkDefaults();
            options.RespectNullableAnnotations = true;
            options.RespectRequiredConstructorParameters = true;
            options.TypeInfoResolver = ApplicationJsonSerializerContext.Default;
        });
    }
}
