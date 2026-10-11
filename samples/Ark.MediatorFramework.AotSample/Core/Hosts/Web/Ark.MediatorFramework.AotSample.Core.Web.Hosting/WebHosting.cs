// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AotSample.Core.Application.DAL;
using Ark.MediatorFramework.AotSample.Core.Application.Host;
using Ark.MediatorFramework.AotSample.Core.Application.Messages;
using Ark.Tools.MediatorFramework.Messaging;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.MediatorFramework.AotSample.Core.Web.Hosting;

/// <summary>The composition every Web process of the sample shares.</summary>
public static class WebHosting
{
    /// <summary>Registers the application and a producer that sends through the SQL outbox.</summary>
    /// <param name="services">The process service collection.</param>
    /// <param name="configuration">The process configuration.</param>
    public static void AddProducer(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddAotApplication(GetSql(configuration));
        services.ConfigureArkMessaging<AotMessagingNetwork>(messaging => messaging
            .Producer<AotApiParticipant>(producer => producer
                .UseTransport(transport => transport.UseServiceBus(new ServiceBusClient(_get(configuration, "ServiceBus"))))
                .UseDataBus(dataBus => dataBus.UseAzureBlob(_dataBusOptions(configuration)))
                .UseOutbox()));
    }

    /// <summary>Registers the application and the receiver of <see cref="AotProcessorParticipant"/>.</summary>
    /// <param name="services">The process service collection.</param>
    /// <param name="configuration">The process configuration.</param>
    public static void AddProcessor(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        var serviceBus = _get(configuration, "ServiceBus");
        var administration = configuration.GetConnectionString("ServiceBusAdministration") ?? serviceBus;
        services.AddAotApplication(GetSql(configuration));
        services.ConfigureArkMessaging<AotMessagingNetwork>(messaging => messaging
            .Receiver<AotProcessorParticipant>(receiver => receiver
                .UseTransport(transport => transport.UseServiceBus(new ServiceBusClient(serviceBus)))
                .UseDataBus(dataBus => dataBus.UseAzureBlob(_dataBusOptions(configuration)))
                .UseResourceManagement(new ServiceBusTransportManagement(new ServiceBusAdministrationClient(administration)))));
    }

    /// <summary>Registers the outbox processor, which moves the outbox rows to Service Bus.</summary>
    /// <param name="services">The process service collection.</param>
    /// <param name="configuration">The process configuration.</param>
    public static void AddOutboxProcessor(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        var serviceBus = _get(configuration, "ServiceBus");
        // A factory registration: the container disposes the transport, and the transport its client.
        services.AddSingleton<IMessagingTransport>(_ => new ServiceBusMessagingTransport(new ServiceBusClient(serviceBus)));
        services.AddArkMessagingOutboxProcessor(new AotDataContextFactory(GetSql(configuration)));
    }

    /// <summary>Gets the SQL Server connection string, <c>ConnectionStrings:Sql</c>.</summary>
    /// <param name="configuration">The process configuration.</param>
    /// <returns>The connection string.</returns>
    public static string GetSql(IConfiguration configuration)
    {
        return _get(configuration, "Sql");
    }

    private static string _get(IConfiguration configuration, string name)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetConnectionString(name)
            ?? throw new InvalidOperationException("ConnectionStrings:" + name + " is required.");
    }

    private static AzureBlobDataBusOptions _dataBusOptions(IConfiguration configuration)
    {
        return new AzureBlobDataBusOptions
        {
            ContainerName = "ark-mediator-aot-databus",
            MinimumAttachmentLifetime = TimeSpan.FromDays(7),
            EnsureContainer = true,
            ConnectionString = _get(configuration, "DataBus"),
        };
    }
}
