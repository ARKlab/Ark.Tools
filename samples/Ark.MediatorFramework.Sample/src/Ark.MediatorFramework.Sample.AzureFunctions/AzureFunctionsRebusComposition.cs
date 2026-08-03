// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Generated;

using Azure.Identity;

using Rebus.Config;
using Rebus.Transport;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.AzureFunctions;

/// <summary>Builds the sample Function host's outbound-only Rebus client.</summary>
public static class AzureFunctionsRebusComposition
{
    /// <summary>
    /// Builds a container that sends owned messages through Azure Service Bus without receiving.
    /// </summary>
    /// <param name="serviceBusConnectionString">
    /// A Service Bus connection string or fully qualified namespace from external configuration.
    /// </param>
    /// <returns>The configured application container.</returns>
    public static Container BuildContainer(string? serviceBusConnectionString)
    {
        if (string.IsNullOrWhiteSpace(serviceBusConnectionString))
            throw new InvalidOperationException(
                "Azure Service Bus configuration is required for the Functions outbound bus.");

        var container = new Container();
        ApplicationComposition.Register(container, useSqlStore: false);
        ApplicationComposition.RegisterOutboundRebus(
            container,
            transport => ConfigureTransport(transport, serviceBusConnectionString),
            ArkGeneratedEndpoints.ConfigureArkRebusRouting<RefreshGreetingCommand>);
        return container;
    }

    private static void ConfigureTransport(
        StandardConfigurer<ITransport> transport,
        string serviceBusConnectionString)
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
