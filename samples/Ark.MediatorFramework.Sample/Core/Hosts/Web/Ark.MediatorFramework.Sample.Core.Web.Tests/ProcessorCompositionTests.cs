// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Web.Hosting;
using Ark.Tools.MediatorFramework;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NodaTime;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Core.Web.Tests;

/// <summary>Composes and verifies each receiver process of the Web variant, as its <c>Program.cs</c> does.</summary>
[TestClass]
public sealed class ProcessorCompositionTests
{
    /// <summary>Each receiver hosts exactly one processor and leaves draining to the outbox processor.</summary>
    /// <param name="processor">The receiver process.</param>
    [TestMethod]
    [DataRow("Processor")]
    [DataRow("NotificationProcessor")]
    [DataRow("AuditProcessor")]
    public async Task EachProcessorHostsExactlyOneReceiver(string processor)
    {
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var transport = new InMemoryMessagingTransport();
        // The attachment lifetime must outlive the network's one-hour maximum scheduling delay.
        var dataBus = new InMemoryMessagingDataBus(SystemClock.Instance, Duration.FromHours(2));
        await using var container = WebHosting.CreateContainer(new ApplicationOptions { DataContextFactory = factory });
        var services = new ServiceCollection();
        services.AddLogging();

        _addReceiver(processor, services, container, transport, dataBus);
        await using var provider = services.BuildServiceProvider();
        WebHosting.BridgeBus(container, () => provider);
        container.Verify();

        var hosted = provider.GetServices<IHostedService>().ToArray();
        hosted.OfType<MessagingProcessorHost>().Should().ContainSingle();
        hosted.OfType<MessagingOutboxProcessor>().Should().BeEmpty();
    }

    private static void _addReceiver(
        string processor,
        IServiceCollection services,
        Container container,
        IMessagingTransport transport,
        IMessagingDataBus dataBus)
    {
        switch (processor)
        {
            case "Processor":
                WebHosting.AddParticipant<SampleMessagingParticipant>(
                    services, container, transport, dataBus, resourceManagement: null, receiver: true);
                break;
            case "NotificationProcessor":
                ApplicationComposition.RegisterNotificationSubscriber(container, new NoOpBookPrintNotificationSink());
                WebHosting.AddParticipant<SampleMessagingNotificationParticipant>(
                    services, container, transport, dataBus, resourceManagement: null, receiver: true);
                break;
            case "AuditProcessor":
                ApplicationComposition.RegisterAuditSubscriber(container, new NoOpBookPrintAuditSink());
                WebHosting.AddParticipant<SampleMessagingAuditParticipant>(
                    services, container, transport, dataBus, resourceManagement: null, receiver: true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(processor), processor, "Unknown processor.");
        }
    }
}
