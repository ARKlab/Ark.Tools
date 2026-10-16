// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies Service Bus outbox batch sends against the local Service Bus emulator.</summary>
[TestClass]
[TestCategory("integration")]
[DoNotParallelize]
public sealed class ServiceBusMessagingBatchSendTests
{
    private const string _queueName = "ark-mf-batch-send";
    private readonly ServiceBusAdministrationClient _administration =
        new(ServiceBusMessagingTransportConformanceTests._sharedAdministrationConnectionString);
    // Held in a list, like the conformance tests, so they are disposed during test cleanup.
    private readonly List<IAsyncDisposable> _owned = [];

    private ServiceBusClient _client => (ServiceBusClient)_owned[0];

    private ServiceBusMessagingTransport _transport => (ServiceBusMessagingTransport)_owned[1];

    [TestInitialize]
    public async Task InitializeQueue()
    {
        await _deleteQueueAsync().ConfigureAwait(false);
        await _administration.CreateQueueAsync(_queueName).ConfigureAwait(false);
        _owned.Add(new ServiceBusClient(ServiceBusMessagingTransportConformanceTests._sharedDataPlaneConnectionString));
#pragma warning disable CA2000 // The transport owns its client and is disposed during test cleanup.
        _owned.Add(new ServiceBusMessagingTransport(
            new ServiceBusClient(ServiceBusMessagingTransportConformanceTests._sharedDataPlaneConnectionString)));
#pragma warning restore CA2000
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        foreach (var owned in _owned)
            await owned.DisposeAsync().ConfigureAwait(false);
        _owned.Clear();
        await _deleteQueueAsync().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task SendBatchDeliversMessagesInOrderWithHeaders()
    {
        var entries = Enumerable.Range(1, 5).Select(static i => _entry("m" + i, [(byte)i])).ToList();

        var durations = await _transport.SendBatchAsync(_queueName, false, entries, default).ConfigureAwait(false);

        durations.Should().HaveCount(5);
        var received = await _receiveAsync(5, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        received.Select(static message => (string)message.ApplicationProperties[MessagingHeaders.MessageId])
            .Should().Equal("m1", "m2", "m3", "m4", "m5");
        received.Select(static message => (string)message.ApplicationProperties["x-custom"])
            .Should().AllBe("custom-value");
        received.Select(static message => message.Body.ToArray()[0]).Should().Equal(1, 2, 3, 4, 5);
    }

    [TestMethod]
    public async Task SendBatchSplitsAcrossServiceBusBatchesInOrder()
    {
        var entries = Enumerable.Range(1, 12)
            .Select(static i => _entry("m" + i.ToString("00", CultureInfo.InvariantCulture), new byte[60 * 1024]))
            .ToList();

        var durations = await _transport.SendBatchAsync(_queueName, false, entries, default).ConfigureAwait(false);

        durations.Should().HaveCount(12);
        var received = await _receiveAsync(12, TimeSpan.FromSeconds(60)).ConfigureAwait(false);
        received.Select(static message => (string)message.ApplicationProperties[MessagingHeaders.MessageId])
            .Should().Equal(entries.Select(static entry => entry.Headers[MessagingHeaders.MessageId]));
    }

    [TestMethod]
    public async Task SendBatchSchedulesMessagesWithADueTime()
    {
        var receiver = _client.CreateReceiver(
            _queueName,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
        await using var __receiver = receiver.ConfigureAwait(false);
        var dueTime = DateTimeOffset.UtcNow.AddSeconds(15);
        var entries = new List<MessagingBatchEntry>
        {
            _entry("m1", [1]),
            _entry("m2", [2]) with { DueTime = dueTime },
        };

        await _transport.SendBatchAsync(_queueName, false, entries, default).ConfigureAwait(false);

        var immediate = await _receiveAsync(receiver, 1, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        immediate.Select(static message => (string)message.ApplicationProperties[MessagingHeaders.MessageId])
            .Should().Equal("m1");
        var early = await receiver.ReceiveMessagesAsync(1, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        early.Should().BeEmpty();
        var peeked = await receiver.PeekMessagesAsync(10).ConfigureAwait(false);
        var scheduled = peeked.Single(static message =>
            (string)message.ApplicationProperties[MessagingHeaders.MessageId] == "m2");
        scheduled.State.Should().Be(ServiceBusMessageState.Scheduled);
        scheduled.ScheduledEnqueueTime.Should().BeCloseTo(dueTime, TimeSpan.FromSeconds(1));
        var late = (await _receiveAsync(receiver, 1, TimeSpan.FromSeconds(60)).ConfigureAwait(false)).Single();
        late.ApplicationProperties[MessagingHeaders.MessageId].Should().Be("m2");
        late.ApplicationProperties["x-custom"].Should().Be("custom-value");
        late.Body.ToArray().Should().Equal(2);
        late.EnqueuedTime.Should().BeOnOrAfter(dueTime.AddSeconds(-1));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task SendBatchRejectsAMessageThatCannotFitAnEmptyBatch()
    {
        var headers = _entry("big", []).Headers;
        var payload = new byte[ServiceBusMessagingTransport.MaximumPayloadSizeBytes - _transport.MeasureNativeHeaders(headers)];
        var entries = new List<MessagingBatchEntry> { new(headers, new ReadOnlySequence<byte>(payload), null) };

        try
        {
            await _transport.SendBatchAsync(_queueName, false, entries, default).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        // The emulator accepted the largest message the size validation allows, so it must be delivered intact.
        var received = await _receiveAsync(1, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        received.Single().Body.ToArray().Should().HaveCount(payload.Length);
    }

    private static MessagingBatchEntry _entry(string messageId, byte[] payload)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingHeaders.MessageId] = messageId,
            ["x-custom"] = "custom-value",
        };
        return new MessagingBatchEntry(headers, new ReadOnlySequence<byte>(payload), null);
    }

    private async Task<List<ServiceBusReceivedMessage>> _receiveAsync(int count, TimeSpan timeout)
    {
        var receiver = _client.CreateReceiver(
            _queueName,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
        await using var __receiver = receiver.ConfigureAwait(false);
        return await _receiveAsync(receiver, count, timeout).ConfigureAwait(false);
    }

    private static async Task<List<ServiceBusReceivedMessage>> _receiveAsync(
        ServiceBusReceiver receiver,
        int count,
        TimeSpan timeout)
    {
        var received = new List<ServiceBusReceivedMessage>();
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (received.Count < count && DateTimeOffset.UtcNow < deadline)
        {
            var remaining = deadline - DateTimeOffset.UtcNow;
            var batch = await receiver.ReceiveMessagesAsync(count - received.Count, remaining).ConfigureAwait(false);
            received.AddRange(batch);
        }

        return received;
    }

    private async Task _deleteQueueAsync()
    {
        if ((await _administration.QueueExistsAsync(_queueName).ConfigureAwait(false)).Value)
            await _administration.DeleteQueueAsync(_queueName).ConfigureAwait(false);
    }
}
