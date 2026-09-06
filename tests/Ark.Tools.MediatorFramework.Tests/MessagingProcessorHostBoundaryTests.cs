// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Azure.Storage.Queues;

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Reusable processor-host checks that only a real broker can answer.</summary>
/// <remarks>
/// The in-memory host tests own the credit algebra; these own the parts the emulators decide:
/// whether a backlog is drained once, whether renewal really extends a native lock a handler
/// outlives, and whether a stop hands unprocessed work back before the lock would have expired.
/// </remarks>
public abstract class MessagingProcessorHostBoundaryTests
{
    /// <summary>Gets the native lock duration the fixture provisions its queue with.</summary>
    protected static TimeSpan LockDuration => TimeSpan.FromSeconds(5);

    /// <summary>Creates the transport under test, locking receives for <see cref="LockDuration"/>.</summary>
    protected abstract IMessagingTransport CreateTransport();

    /// <summary>Gets the queue the host processes.</summary>
    protected abstract string QueueName { get; }

    [TestMethod]
    public async Task HostDrainsABacklogExactlyOnceAndLeavesTheQueueEmpty()
    {
        const int count = 20;
        var transport = CreateTransport();
        var source = (IMessagingMessageSource)transport;
        for (var index = 0; index < count; index++)
            await _sendAsync(transport, index).ConfigureAwait(false);

        var received = new ConcurrentBag<string>();
        var host = new MessagingProcessorHost(
            source,
            QueueName,
            async (delivery, ctk) =>
            {
                received.Add(delivery.Headers["n"]);
                await delivery.CompleteAsync(ctk).ConfigureAwait(false);
            },
            _options(4));
        await using var scope = host.ConfigureAwait(false);

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await _waitUntilAsync(() => received.Count >= count).ConfigureAwait(false);
        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);

        received.Should().BeEquivalentTo(
            Enumerable.Range(0, count).Select(static index => index.ToString(CultureInfo.InvariantCulture)),
            "every message is dispatched once and settled");
        var leftover = await source
            .ReceiveBatchAsync(QueueName, count, TimeSpan.FromSeconds(1), default)
            .ConfigureAwait(false);
        leftover.Should().BeEmpty("a completed delivery is removed from the broker");
    }

    [TestMethod]
    public async Task HostRenewsTheLockOfAHandlerThatOutlivesIt()
    {
        var transport = CreateTransport();
        var source = (IMessagingMessageSource)transport;
        await _sendAsync(transport, 0).ConfigureAwait(false);

        var attempts = 0;
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new MessagingProcessorHost(
            source,
            QueueName,
            async (delivery, ctk) =>
            {
                Interlocked.Increment(ref attempts);
                await Task.Delay(LockDuration * 1.5, ctk).ConfigureAwait(false);
                await delivery.CompleteAsync(ctk).ConfigureAwait(false);
                settled.TrySetResult();
            },
            _options(1));
        await using var scope = host.ConfigureAwait(false);

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await settled.Task.WaitAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(false);
        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);

        // Without renewal the lock expires mid-handler and the broker hands the message to the
        // host a second time, which is exactly what a second attempt would mean here.
        attempts.Should().Be(1, "the renewer keeps the lock alive while the handler runs");
        var leftover = await source
            .ReceiveBatchAsync(QueueName, 1, TimeSpan.FromSeconds(1), default)
            .ConfigureAwait(false);
        leftover.Should().BeEmpty("a renewed lock still settles");
    }

    [TestMethod]
    public async Task StoppingAbandonsBufferedWorkBeforeTheLockWouldExpire()
    {
        const int count = 8;
        var transport = CreateTransport();
        var source = (IMessagingMessageSource)transport;
        for (var index = 0; index < count; index++)
            await _sendAsync(transport, index).ConfigureAwait(false);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = _options(2);
        options.ShutdownTimeout = TimeSpan.FromSeconds(1);
        var host = new MessagingProcessorHost(
            source,
            QueueName,
            async (delivery, ctk) =>
            {
                await gate.Task.WaitAsync(ctk).ConfigureAwait(false);
                await delivery.CompleteAsync(ctk).ConfigureAwait(false);
            },
            options);
        await using var scope = host.ConfigureAwait(false);

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await _waitUntilAsync(() => host.Outstanding == host.PrefetchBudget).ConfigureAwait(false);
        // A full budget is also reached while the last credits are held by a receive still in
        // flight, so let that batch reach the buffer before stopping.
        await Task.Delay(500).ConfigureAwait(false);
        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        var stopped = Stopwatch.StartNew();

        host.AbandonedOnShutdown.Should().BeGreaterThan(0, "the buffer still held deliveries no worker reached");
        var redelivered = await _receiveAllAsync(source, host.AbandonedOnShutdown).ConfigureAwait(false);
        stopped.Elapsed.Should().BeLessThan(LockDuration, "abandoning makes redelivery immediate rather than lock-expiry-delayed");
        foreach (var delivery in redelivered)
            await delivery.CompleteAsync(default).ConfigureAwait(false);
    }

    private async Task _sendAsync(IMessagingTransport transport, int index)
    {
        await transport.SendAsync(
            QueueName,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["n"] = index.ToString(CultureInfo.InvariantCulture)
            },
            new ReadOnlySequence<byte>(new byte[] { (byte)index }),
            null,
            default).ConfigureAwait(false);
    }

    private async Task<List<IMessagingLockedDelivery>> _receiveAllAsync(IMessagingMessageSource source, int expected)
    {
        var deliveries = new List<IMessagingLockedDelivery>(expected);
        while (deliveries.Count < expected)
        {
            var batch = await source
                .ReceiveBatchAsync(QueueName, expected - deliveries.Count, TimeSpan.FromSeconds(1), default)
                .ConfigureAwait(false);
            deliveries.AddRange(batch);
            if (batch.Count == 0)
                await Task.Delay(50).ConfigureAwait(false);
        }

        return deliveries;
    }

    private static MessagingProcessingOptions _options(int concurrency)
    {
        return new MessagingProcessingOptions
        {
            InitialConcurrency = concurrency,
            MinimumConcurrency = concurrency,
            MaximumConcurrency = concurrency,
            AdaptiveConcurrency = false,
            ReceiveWaitTime = TimeSpan.FromMilliseconds(250),
            MinPollInterval = TimeSpan.FromMilliseconds(10),
            MaxPollInterval = TimeSpan.FromMilliseconds(100),
            // The defaults assume a lock measured in minutes; the emulator queues use seconds so the
            // renewer still gets a scan and a margin inside the lock.
            RenewalSafetyMargin = TimeSpan.FromSeconds(2),
            RenewalScanInterval = TimeSpan.FromMilliseconds(250)
        };
    }

    private static async Task _waitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, timeout.Token).ConfigureAwait(false);
        }
    }
}

/// <summary>Runs the processor-host boundary suite against the repository Azurite instance.</summary>
[TestClass]
[TestCategory("integration")]
public sealed class StorageQueueMessagingProcessorHostBoundaryTests : MessagingProcessorHostBoundaryTests
{
    private const string _queue = "amf-host-boundary";
    private readonly QueueServiceClient _service = new(
        "UseDevelopmentStorage=true",
        new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.None
        });

    protected override string QueueName => _queue;

    protected override IMessagingTransport CreateTransport()
    {
        return new StorageQueueMessagingTransport(
            _service,
            receiveVisibilityTimeout: LockDuration,
            retryDelay: TimeSpan.FromMilliseconds(100));
    }

    [TestInitialize]
    public async Task CreateQueue()
    {
        var client = _service.GetQueueClient(_queue);
        await client.DeleteIfExistsAsync().ConfigureAwait(false);
        await client.CreateIfNotExistsAsync().ConfigureAwait(false);
    }

    [TestCleanup]
    public async Task DeleteQueue()
    {
        await _service.GetQueueClient(_queue).DeleteIfExistsAsync().ConfigureAwait(false);
    }
}

/// <summary>Runs the processor-host boundary suite against the local Service Bus emulator.</summary>
[TestClass]
[TestCategory("integration")]
[DoNotParallelize]
public sealed class ServiceBusMessagingProcessorHostBoundaryTests : MessagingProcessorHostBoundaryTests
{
    private const string _defaultAdministrationConnectionString = "Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
    private const string _queue = "ark-mf-host-boundary";
    private static readonly string _administrationConnectionString = _serviceBusConnectionString();
    private static readonly string _connectionString = _dataPlaneConnectionString(_administrationConnectionString);
    private readonly ServiceBusAdministrationClient _administration = new(_administrationConnectionString);
    private readonly List<ServiceBusMessagingTransport> _transports = new();

    protected override string QueueName => _queue;

    protected override IMessagingTransport CreateTransport()
    {
#pragma warning disable CA2000 // The tracked transport owns and disposes the client during test cleanup.
        var transport = new ServiceBusMessagingTransport(
            new ServiceBusClient(_connectionString),
            lockDuration: LockDuration);
#pragma warning restore CA2000
        _transports.Add(transport);
        return transport;
    }

    [TestInitialize]
    public async Task CreateQueue()
    {
        await _deleteQueueAsync().ConfigureAwait(false);
        _ = await _administration.CreateQueueAsync(
            new CreateQueueOptions(_queue)
            {
                LockDuration = LockDuration
            }).ConfigureAwait(false);
    }

    [TestCleanup]
    public async Task DisposeTransports()
    {
        foreach (var transport in _transports)
            await transport.DisposeAsync().ConfigureAwait(false);
        _transports.Clear();

        await _deleteQueueAsync().ConfigureAwait(false);
    }

    private async Task _deleteQueueAsync()
    {
        if ((await _administration.QueueExistsAsync(_queue).ConfigureAwait(false)).Value)
            await _administration.DeleteQueueAsync(_queue).ConfigureAwait(false);
    }

    private static string _serviceBusConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ARK_SERVICEBUS_EMULATOR_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        return _defaultAdministrationConnectionString;
    }

    private static string _dataPlaneConnectionString(string connectionString)
    {
        const string endpointPrefix = "Endpoint=";
        var endpointStart = connectionString.IndexOf(endpointPrefix, StringComparison.Ordinal)
            + endpointPrefix.Length;
        var endpointEnd = connectionString.IndexOf(';', endpointStart);
        var endpoint = new Uri(connectionString[endpointStart..endpointEnd]);
        var dataPlaneEndpoint = new UriBuilder(endpoint) { Port = -1 }.Uri
            .AbsoluteUri.TrimEnd('/');
        return connectionString[..endpointStart] + dataPlaneEndpoint + connectionString[endpointEnd..];
    }
}
