// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.WebRebus.AuditProcessor;
using Ark.MediatorFramework.Sample.Core.WebRebus.Hosting;
using Ark.MediatorFramework.Sample.Core.WebRebus.NotificationProcessor;
using Ark.MediatorFramework.Sample.Core.WebRebus.Processor;
using Ark.Tools.Outbox;
using Ark.Tools.Rebus.Tests;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Rebus.DataBus.InMem;
using Rebus.Transport.InMem;

namespace Ark.MediatorFramework.Sample.Core.WebRebus.Tests;

/// <summary>Hosts the Rebus receivers as isolated hosted services.</summary>
[TestClass]
public sealed class ReceiverHostTests
{
    /// <summary>One process can host every receiver, each with its own container and bus.</summary>
    [TestMethod]
    public async Task OneProcessHostsEveryReceiver()
    {
        var network = new InMemNetwork();
        var subscribers = new InMemorySubscriberStore();
        var dataStore = new InMemDataStore();
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var options = new RebusReceiverHostOptions
        {
            Application = new ApplicationOptions { DataContextFactory = factory },
            // The shared subscriber store replaces the per-transport one, as Azure Service Bus topics do in production.
            Transport = (transport, queue) => transport.UseInMemoryTransport(network, queue, registerSubscriptionStorage: false),
            DataBus = storage => storage.StoreInMemory(dataStore),
            Configure = configurer =>
            {
                configurer.Subscriptions(s => s.StoreInMemory(subscribers));
                configurer.Timeouts(static t => t.StoreInMemoryTests());
            },
        };
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddRebusReceiverHost<WorkerRebusHost>(_ => options, startOutboxProcessor: true);
        builder.Services.AddRebusReceiverHost<NotificationRebusHost>(
            _ => options,
            static container => ApplicationComposition.RegisterNotificationSubscriber(container, new NoOpBookPrintNotificationSink()));
        builder.Services.AddRebusReceiverHost<AuditRebusHost>(
            _ => options,
            static container => ApplicationComposition.RegisterAuditSubscriber(container, new NoOpBookPrintAuditSink()));
        using var host = builder.Build();

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var containers = host.Services.GetServices<IHostedService>()
                .Select(static hosted => hosted switch
                {
                    RebusReceiverHost<WorkerRebusHost> worker => worker.Container,
                    RebusReceiverHost<NotificationRebusHost> notification => notification.Container,
                    RebusReceiverHost<AuditRebusHost> audit => audit.Container,
                    _ => null,
                })
                .OfType<SimpleInjector.Container>()
                .ToArray();
            containers.Should().HaveCount(3).And.OnlyHaveUniqueItems();
            containers.Select(static container => container.GetInstance<global::Rebus.Bus.IBus>())
                .Should().OnlyHaveUniqueItems();
        }
        finally
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
