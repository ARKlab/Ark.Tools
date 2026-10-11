// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Web.Hosting;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NodaTime;

namespace Ark.MediatorFramework.Sample.Core.Web.Tests;

/// <summary>Composes and starts each receiver of the Web variant as an isolated hosted service, as its <c>Program.cs</c> does.</summary>
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
        var services = new ServiceCollection();
        services.AddLogging();
        _addReceiver(processor, services, _options());
        var provider = services.BuildServiceProvider();
        await using var __provider = provider.ConfigureAwait(false);
        var host = provider.GetServices<IHostedService>().Should().ContainSingle()
            .Which.Should().BeAssignableTo<IsolatedMessagingHost>().Subject;

        // Starting verifies the receiver's own application container.
        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var hosted = host.Services.GetServices<IHostedService>().ToArray();
            hosted.OfType<MessagingProcessorHost>().Should().ContainSingle();
            hosted.OfType<MessagingOutboxProcessor>().Should().BeEmpty();
        }
        finally
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>One process can host every receiver and the outbox processor, each isolated from the others.</summary>
    [TestMethod]
    public async Task OneProcessHostsEveryMessagingHost()
    {
        var options = _options();
        var builder = Host.CreateApplicationBuilder();
        _addReceiver("Processor", builder.Services, options);
        _addReceiver("NotificationProcessor", builder.Services, options);
        _addReceiver("AuditProcessor", builder.Services, options);
        builder.Services.AddOutboxProcessorHost(
            _ => options.Transport(),
            static _ => new InMemoryOutboxContextFactory());
        using var host = builder.Build();

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var hosts = host.Services.GetServices<IHostedService>().OfType<IsolatedMessagingHost>().ToArray();
            hosts.Should().HaveCount(4);
            hosts.Select(static h => h.Services).Should().OnlyHaveUniqueItems();
            hosts.SelectMany(static h => h.Services.GetServices<IHostedService>())
                .OfType<MessagingProcessorHost>().Should().HaveCount(3);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static MessagingHostOptions _options()
    {
        // One in-memory transport and DataBus stand for the shared Service Bus namespace and blob container.
        var transport = new InMemoryMessagingTransport();
        return new MessagingHostOptions
        {
            Application = new ApplicationOptions
            {
                DataContextFactory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory()),
            },
            Transport = () => transport,
            // The attachment lifetime must outlive the network's one-hour maximum scheduling delay.
            DataBus = new InMemoryMessagingDataBus(SystemClock.Instance, Duration.FromHours(2)),
        };
    }

    private static void _addReceiver(string processor, IServiceCollection services, MessagingHostOptions options)
    {
        switch (processor)
        {
            case "Processor":
                services.AddReceiverHost<SampleMessagingParticipant>(_ => options);
                break;
            case "NotificationProcessor":
                services.AddReceiverHost<SampleMessagingNotificationParticipant>(
                    _ => options,
                    static container => ApplicationComposition.RegisterNotificationSubscriber(container, new NoOpBookPrintNotificationSink()));
                break;
            case "AuditProcessor":
                services.AddReceiverHost<SampleMessagingAuditParticipant>(
                    _ => options,
                    static container => ApplicationComposition.RegisterAuditSubscriber(container, new NoOpBookPrintAuditSink()));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(processor), processor, "Unknown processor.");
        }
    }
}
