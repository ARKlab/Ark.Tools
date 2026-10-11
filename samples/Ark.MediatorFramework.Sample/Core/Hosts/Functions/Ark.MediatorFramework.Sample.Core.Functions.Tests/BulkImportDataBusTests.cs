// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;

using AwesomeAssertions;

using NodaTime;

namespace Ark.MediatorFramework.Sample.Core.Functions.Tests;

/// <summary>Sends an oversized bulk import from the Api app to the Processor app's trigger, over in-memory seams.</summary>
[TestClass]
public sealed class BulkImportDataBusTests
{
    /// <summary>
    /// A bulk import bigger than the transport inline limit travels as a DataBus claim check and the Processor app
    /// creates every book.
    /// </summary>
    [TestMethod]
    public async Task OversizedBulkImportTravelsThroughTheDataBus()
    {
        var store = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var transport = new InMemoryMessagingTransport();
        // The attachment lifetime must outlive the network's one-hour maximum scheduling delay.
        var dataBus = new InMemoryMessagingDataBus(SystemClock.Instance, Duration.FromHours(2));
        await using var api = await FunctionsTestHosts.StartApiProducerAsync(
            builder => builder.Use(transport),
            builder => builder.Use(dataBus),
            store).ConfigureAwait(false);
        await using var worker = FunctionsTestHosts.ComposeWorkerTrigger(dataBus, store);
        var author = PersonName.From("Bulk " + Guid.NewGuid().ToString("N"));

        await api.Bus.Send(FunctionsTestHosts.NewOversizedBulkImport(author)).ConfigureAwait(false);
        var deliveries = await transport.ReceiveBatchAsync(
            FunctionsTestHosts.WorkerQueueName(), maxMessages: 1, TimeSpan.FromSeconds(5), CancellationToken.None)
            .ConfigureAwait(false);
        deliveries.Should().ContainSingle();
        deliveries[0].Headers.Should().ContainKey(MessagingHeaders.PayloadAttachmentId, "the body travels as a claim check, not inline");
        await worker.DeliverAsync(deliveries[0]).ConfigureAwait(false);

        (await FunctionsTestHosts.CountBooksAsync(store, author).ConfigureAwait(false))
            .Should().Be(FunctionsTestHosts.BulkImportSize);
        dataBus.Count.Should().Be(1);
    }
}
