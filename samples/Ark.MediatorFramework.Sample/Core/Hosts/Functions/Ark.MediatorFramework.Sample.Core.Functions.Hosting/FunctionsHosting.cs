// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Authorization;
using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Application.JsonContext;
using Ark.MediatorFramework.Sample.Core.Application.Messages;
using Ark.Tools.MediatorFramework.AzureFunctions;
using Ark.Tools.MediatorFramework.AzureFunctions.SimpleInjector;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;
using Ark.Tools.Solid.SimpleInjector;

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

    /// <summary>Composes a trigger app: its generated Service Bus trigger, enqueue-only outbox, and container bridge.</summary>
    /// <remarks>
    /// The order matters: the user context goes before <c>AddArkAzureFunctions</c> so its
    /// <c>TryAdd</c> keeps the message principal, and the processors are bridged before
    /// <c>ConfigureArkMessagingFunctions</c> validates the generated handlers against the container.
    /// </remarks>
    /// <param name="services">The Functions service collection.</param>
    /// <param name="configuration">The app configuration.</param>
    /// <param name="manifest">The app's generated messaging manifest.</param>
    /// <param name="container">The application container.</param>
    public static void AddMessagingTrigger(
        IServiceCollection services,
        IConfiguration configuration,
        MessagingFunctionsManifest manifest,
        Container container)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(container);
        _configureMessagingJson(services);
        AddUserContext(services, container);
        services.AddArkAzureFunctions();
        services.AddArkSolidProcessors(container);
        services.ConfigureArkMessagingFunctions(
            configuration,
            manifest,
            messaging => messaging
                .UseTransport(static transport => transport.UseServiceBus())
                .UseDataBus(dataBus => dataBus.UseAzureBlob(DataBusOptions(configuration)))
                .UseOutbox(static outbox => outbox.UseEnqueue()));
        services.AddArkAzureFunctionsSimpleInjectorBridge(container);
    }

    /// <summary>Composes the Api app's producer: it sends through the outbox and receives nothing.</summary>
    /// <remarks>
    /// The outgoing user is the HTTP user that <c>AddArkAzureFunctions</c> registers, unless the
    /// caller registered another <see cref="IContextProvider{TItem}"/> of <see cref="ClaimsPrincipal"/> first.
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
                .UseOutbox()));
        services.AddArkAzureFunctionsSimpleInjectorBridge(container);
    }

    private static void _configureMessagingJson(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Configure<JsonSerializerOptions>(static options =>
        {
            options.RespectNullableAnnotations = true;
            options.RespectRequiredConstructorParameters = true;
            options.TypeInfoResolver = ApplicationJsonSerializerContext.Default;
        });
    }
}
