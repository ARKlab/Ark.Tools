// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.WebRebus.AuditProcessor;
using Ark.MediatorFramework.Sample.Core.WebRebus.Hosting;
using Ark.MediatorFramework.Sample.Core.WebRebus.NotificationProcessor;
using Ark.MediatorFramework.Sample.Core.WebRebus.Processor;
using Ark.MediatorFramework.Sample.Core.WebRebus.WebInterface;
using Ark.Tools.MediatorFramework.Rebus;
using Ark.Tools.Rebus;
using Ark.Tools.Rebus.Tests;
using Ark.Tools.Solid;

using Rebus.DataBus.InMem;
using Rebus.Transport.InMem;

using SimpleInjector;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Core.WebRebus.Tests;

/// <summary>Composes each WebRebus process as its <c>Program.cs</c> does, over in-memory Rebus.</summary>
internal static class WebRebusTestHosts
{
    /// <summary>Starts the api process: a one-way client with a settable user.</summary>
    public static async Task<RebusTestProcess> ApiAsync(
        InMemNetwork network,
        InMemorySubscriberStore subscribers,
        InMemDataStore dataStore,
        InMemorySampleDataContextFactory factory)
    {
        var container = RebusHosting.CreateContainer(new ApplicationOptions { DataContextFactory = factory });
        var principal = new SettablePrincipalProvider();
        container.RegisterInstance<IContextProvider<ClaimsPrincipal>>(principal);
        RebusHosting.Configure<ApiRebusHost>(
            container,
            t => t.UseDrainableInMemoryTransportAsOneWayClient(network),
            d => d.StoreInMemory(dataStore),
            startOutboxProcessor: false,
            configureTest: cfg => cfg.Subscriptions(s => s.StoreInMemory(subscribers)));
        return await _startAsync<ApiRebusHost>(container, principal).ConfigureAwait(false);
    }

    /// <summary>Starts the print worker, which also drains the shared outbox.</summary>
    public static async Task<RebusTestProcess> WorkerAsync(
        InMemNetwork network,
        InMemorySubscriberStore subscribers,
        InMemDataStore dataStore,
        InMemorySampleDataContextFactory factory)
    {
        var container = RebusHosting.CreateContainer(new ApplicationOptions { DataContextFactory = factory });
        return await _receiverAsync<WorkerRebusHost>(
            container, network, subscribers, dataStore, SampleMessagingParticipant.Identity, startOutboxProcessor: true)
            .ConfigureAwait(false);
    }

    /// <summary>Starts the notification subscriber.</summary>
    public static async Task<RebusTestProcess> NotificationAsync(
        InMemNetwork network,
        InMemorySubscriberStore subscribers,
        InMemDataStore dataStore,
        InMemorySampleDataContextFactory factory,
        IBookPrintNotificationSink sink)
    {
        var container = RebusHosting.CreateContainer(new ApplicationOptions { DataContextFactory = factory });
        ApplicationComposition.RegisterNotificationSubscriber(container, sink);
        return await _receiverAsync<NotificationRebusHost>(
            container, network, subscribers, dataStore, SampleMessagingNotificationParticipant.Identity, startOutboxProcessor: false)
            .ConfigureAwait(false);
    }

    /// <summary>Starts the audit subscriber.</summary>
    public static async Task<RebusTestProcess> AuditAsync(
        InMemNetwork network,
        InMemorySubscriberStore subscribers,
        InMemDataStore dataStore,
        InMemorySampleDataContextFactory factory,
        IBookPrintAuditSink sink)
    {
        var container = RebusHosting.CreateContainer(new ApplicationOptions { DataContextFactory = factory });
        ApplicationComposition.RegisterAuditSubscriber(container, sink);
        return await _receiverAsync<AuditRebusHost>(
            container, network, subscribers, dataStore, SampleMessagingAuditParticipant.Identity, startOutboxProcessor: false)
            .ConfigureAwait(false);
    }

    /// <summary>Creates a valid book.</summary>
    public static Book_CreateRequest.V1 NewBook()
    {
        return new Book_CreateRequest.V1(new Book.V1.Create
        {
            Title = "Dune",
            Author = "Herbert",
            Genre = Book.V1.Genre.Fiction,
        });
    }

    /// <summary>Polls every 50 ms until the condition holds.</summary>
    /// <exception cref="TimeoutException">The condition did not hold within 5 seconds.</exception>
    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The condition did not hold within 5 seconds.");
            await Task.Delay(50).ConfigureAwait(false);
        }
    }

    private static async Task<RebusTestProcess> _receiverAsync<THost>(
        Container container,
        InMemNetwork network,
        InMemorySubscriberStore subscribers,
        InMemDataStore dataStore,
        string queue,
        bool startOutboxProcessor)
        where THost : IArkRebusHost
    {
        container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, RebusPrincipalContextWithFallbackProvider>();
        RebusHosting.Configure<THost>(
            container,
            // The shared subscriber store replaces the per-transport one, as Azure Service Bus topics do in production.
            t => t.UseInMemoryTransport(network, queue, registerSubscriptionStorage: false),
            d => d.StoreInMemory(dataStore),
            startOutboxProcessor,
            static options => options.AddInProcessMessageInspector(),
            cfg =>
            {
                cfg.Subscriptions(s => s.StoreInMemory(subscribers));
                cfg.Timeouts(static t => t.StoreInMemoryTests());
            });
        return await _startAsync<THost>(container, principal: null).ConfigureAwait(false);
    }

    private static async Task<RebusTestProcess> _startAsync<THost>(Container container, SettablePrincipalProvider? principal)
        where THost : IArkRebusHost
    {
        container.Verify();
        container.StartBus();
        await THost.SubscribeAsync(container.GetInstance<global::Rebus.Bus.IBus>()).ConfigureAwait(false);
        return new RebusTestProcess(container, principal);
    }
}
