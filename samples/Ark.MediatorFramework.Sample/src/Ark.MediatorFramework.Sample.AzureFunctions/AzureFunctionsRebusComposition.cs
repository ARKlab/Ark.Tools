// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application.Messages;

using Ark.Tools.MediatorFramework.Rebus;
using Ark.Tools.Solid.Authorization;

#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

using Azure.Identity;

using Rebus.Config;
using Rebus.Transport;

using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Ark.MediatorFramework.Sample.AzureFunctions;

/// <summary>Generated outbound Rebus host for the sample Function application.</summary>
[ArkRebusHost(typeof(SampleMessagingPublisherParticipant))]
public sealed partial class AzureFunctionsRebusHost;

/// <summary>Builds the sample Function host's outbound-only Rebus client.</summary>
public static class AzureFunctionsRebusComposition
{
    /// <summary>
    /// Adds the outbound-only Rebus client to an existing Function application
    /// container without replacing its native messaging composition.
    /// </summary>
    /// <param name="container">The already composed Function application container.</param>
    /// <param name="serviceBusConnectionString">
    /// A Service Bus connection string or fully qualified namespace from external configuration.
    /// </param>
    public static void ConfigureOutbound(
        Container container,
#if NET10_0_OR_GREATER
        [InfrastructureSecret] string? serviceBusConnectionString)
#else
        string? serviceBusConnectionString)
#endif
    {
        ArgumentNullException.ThrowIfNull(container);

        if (string.IsNullOrWhiteSpace(serviceBusConnectionString))
            throw new InvalidOperationException(
                "Azure Service Bus configuration is required for the Functions outbound bus.");

        var requirements = AzureFunctionsRebusHost.GetRequirements();
        AzureFunctionsRebusHost.Register(
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
        ApplicationComposition.RegisterOutboundRebus(
            container,
            transport => _configureTransport(transport, serviceBusConnectionString),
            AzureFunctionsRebusHost.ConfigureRouting);
    }

    /// <summary>
    /// Builds a container that sends owned messages through Azure Service Bus without receiving.
    /// </summary>
    /// <param name="serviceBusConnectionString">
    /// A Service Bus connection string or fully qualified namespace from external configuration.
    /// </param>
    /// <param name="useSqlStore">Whether to use the shared SQL persistence profile.</param>
    /// <param name="connectionString">Optional SQL Server connection string.</param>
    /// <returns>The configured application container.</returns>
    public static Container BuildContainer(
#if NET10_0_OR_GREATER
        [InfrastructureSecret] string? serviceBusConnectionString,
#else
        string? serviceBusConnectionString,
#endif
        bool useSqlStore = false,
#if NET10_0_OR_GREATER
        [InfrastructureSecret] string? connectionString = null)
#else
        string? connectionString = null)
#endif
    {
        if (string.IsNullOrWhiteSpace(serviceBusConnectionString))
            throw new InvalidOperationException(
                "Azure Service Bus configuration is required for the Functions outbound bus.");

        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        ApplicationComposition.Register(
            container,
            useSqlStore,
            connectionString);
        container.RegisterAuthorization();
        container.RegisterAuthorizationHandler<ScopeAuthorizationHandler>();
        ConfigureOutbound(container, serviceBusConnectionString);
        return container;
    }

    private static void _configureTransport(
        StandardConfigurer<ITransport> transport,
#if NET10_0_OR_GREATER
        [InfrastructureSecret] string serviceBusConnectionString)
#else
        string serviceBusConnectionString)
#endif
    {
        if (serviceBusConnectionString.Contains("SharedAccess", StringComparison.OrdinalIgnoreCase))
        {
            transport.UseAzureServiceBusAsOneWayClient(serviceBusConnectionString);
            return;
        }

        transport.UseAzureServiceBusAsOneWayClient(
            serviceBusConnectionString,
            new DefaultAzureCredential());
    }
}
