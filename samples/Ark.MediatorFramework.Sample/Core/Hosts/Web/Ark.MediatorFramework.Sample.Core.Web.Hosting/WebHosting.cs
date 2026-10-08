// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Authorization;
using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Application.JsonContext;
using Ark.MediatorFramework.Sample.Core.Application.Messages;
using Ark.Tools.MediatorFramework;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;
using Ark.Tools.Solid.Authorization;
using Ark.Tools.Solid.SimpleInjector;

using Azure.Messaging.ServiceBus.Administration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Security.Claims;
using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Core.Web.Hosting;

/// <summary>Composition shared by every process of the Web variant.</summary>
public static class WebHosting
{
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

    /// <summary>Registers one messaging participant.</summary>
    /// <typeparam name="TParticipant">The participant hosted by this process.</typeparam>
    /// <param name="services">The process service collection.</param>
    /// <param name="container">The application container.</param>
    /// <param name="transport">The messaging transport.</param>
    /// <param name="dataBus">The claim-check DataBus.</param>
    /// <param name="resourceManagement">
    /// Provisions the participant's published topics and, for a receiver, its queue and subscriptions
    /// (<c>CreateIfMissing</c>). Production passes
    /// <c>ServiceBusTransportManagement</c>; tests pass <see langword="null"/> because the in-memory
    /// transport manages its own resources.
    /// </param>
    /// <param name="receiver"><see langword="true"/> to host a processor; <see langword="false"/> for a producer.</param>
    public static void AddParticipant<TParticipant>(
        IServiceCollection services,
        Container container,
        IMessagingTransport transport,
        IMessagingDataBus dataBus,
        IMessagingTransportManagement? resourceManagement,
        bool receiver)
        where TParticipant : class, IMessagingParticipant<TParticipant>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);
        services.Configure<JsonSerializerOptions>(static options =>
        {
            // The Ark defaults (camelCase, EvolvableEnum and NodaTime converters) match the application contracts.
            options.ConfigureArkDefaults();
            options.RespectNullableAnnotations = true;
            options.RespectRequiredConstructorParameters = true;
            options.TypeInfoResolver = ApplicationJsonSerializerContext.Default;
        });
        services.AddArkSolidProcessors(container);
        if (receiver)
        {
            var principal = new MessagingPrincipalContextProvider();
            container.RegisterInstance<IContextProvider<ClaimsPrincipal>>(principal);
            // Steps are resolved by type from DI (MessagingPipelineInvoker); register the
            // instances before ConfigureArkMessaging so a framework TryAdd keeps them.
            services.AddSingleton(new UserContextIncomingStep(principal.Set));
            services.AddSingleton(new UserContextOutgoingStep(() => principal.Current));
            services.ConfigureArkMessaging(
                SampleMessagingNetwork.CreateOptions(),
                SampleMessagingNetwork.Registry,
                messaging => messaging.Receiver<TParticipant>(r =>
                {
                    r.UseTransport(transport)
                        .UseDataBus(dataBus)
                        .UseIncomingPipeline(typeof(UserContextIncomingStep))
                        .UseOutgoingPipeline(typeof(UserContextOutgoingStep))
                        .UseOutbox();
                    if (resourceManagement is not null)
                        r.UseResourceManagement(resourceManagement);
                }));
        }
        else
        {
            services.AddSingleton(new UserContextOutgoingStep(
                () => container.GetInstance<IContextProvider<ClaimsPrincipal>>().Current));
            services.ConfigureArkMessaging(
                SampleMessagingNetwork.CreateOptions(),
                SampleMessagingNetwork.Registry,
                messaging => messaging.Producer<TParticipant>(p =>
                {
                    p.UseTransport(transport)
                        .UseDataBus(dataBus)
                        .UseOutgoingPipeline(typeof(UserContextOutgoingStep))
                        .UseOutbox();
                    if (resourceManagement is not null)
                        p.UseResourceManagement(resourceManagement);
                }));
        }
    }

    /// <summary>Creates the Service Bus resource manager that provisions published topics.</summary>
    /// <remarks>
    /// Reads <c>ConnectionStrings:ServiceBusAdministration</c>, falling back to
    /// <c>ConnectionStrings:ServiceBus</c>. A namespace uses one connection for both; the local
    /// emulator serves administration on a different port, so development settings set both keys.
    /// </remarks>
    /// <param name="configuration">The process configuration.</param>
    /// <returns>The resource manager.</returns>
    public static IMessagingTransportManagement CreateResourceManagement(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connection = configuration.GetConnectionString("ServiceBusAdministration")
            ?? configuration.GetConnectionString("ServiceBus")
            ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");
        return new ServiceBusTransportManagement(new ServiceBusAdministrationClient(connection));
    }

    /// <summary>Creates the claim-check DataBus shared by every Web process.</summary>
    /// <remarks>
    /// Every process of the variant must read the attachments the others write, so the store is
    /// Azure Blob Storage, never the process-local in-memory DataBus. Locally the connection points
    /// at Azurite from docker-compose.
    /// </remarks>
    /// <param name="configuration">The process configuration.</param>
    /// <returns>The shared DataBus.</returns>
    public static IMessagingDataBus CreateDataBus(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new AzureBlobMessagingDataBus(new AzureBlobDataBusOptions
        {
            ContainerName = "amf1-databus",
            Prefix = "sample/",
            MinimumAttachmentLifetime = TimeSpan.FromDays(7),
            EnsureContainer = true,
            ConnectionString = configuration.GetConnectionString("DataBus")
                ?? throw new InvalidOperationException("ConnectionStrings:DataBus is required."),
        });
    }

    /// <summary>Exposes the participant bus, which lives in Microsoft DI, to the application container.</summary>
    /// <param name="container">The application container.</param>
    /// <param name="services">Returns the built root provider; called lazily on first resolution.</param>
    public static void BridgeBus(Container container, Func<IServiceProvider> services)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(services);
        container.RegisterSingleton<IBus>(() => services().GetRequiredService<IBus>());
        container.RegisterSingleton<IBusOutboxEnlistment>(() => services().GetRequiredService<IBusOutboxEnlistment>());
    }
}
