// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies native messaging transactional outbox behavior.</summary>
[TestClass]
public sealed partial class MessagingOutboxTests
{
    [TestMethod]
    public async Task ScopedOperationsPersistValidatedEnvelopesOnCompletion()
    {
        var transport = new RecordingTransport();
        using var bus = _createBus(transport);
        var factory = new InMemoryOutboxContextFactory();
        var context = await factory.CreateAsync().ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);
        using (var scope = bus.Enlist(context))
        {
            await bus.Send(
                new TestMessage { Value = "send" },
                new Dictionary<string, string>(StringComparer.Ordinal) { ["tenant"] = "books" })
                .ConfigureAwait(false);
            await bus.Defer(
                new TestMessage { Value = "scheduled" },
                DateTimeOffset.Parse("2024-01-01T00:05:00Z", CultureInfo.InvariantCulture),
                cancellationToken: default).ConfigureAwait(false);
            await bus.Publish(new TestEvent { Value = "publish" }).ConfigureAwait(false);
            transport.Sends.Should().BeEmpty();
            transport.Publishes.Should().BeEmpty();

            await scope.CompleteAsync().ConfigureAwait(false);
        }
        await context.CommitAsync().ConfigureAwait(false);

        var inspection = await factory.CreateAsync().ConfigureAwait(false);
        await using var __inspection = inspection.ConfigureAwait(false);
        var messages = (await inspection.PeekLockMessagesAsync(10).ConfigureAwait(false)).ToList();
        messages.Should().HaveCount(3);
        messages.Should().OnlyContain(static message =>
            message.Headers![MessagingHeaders.SenderIdentity] == "sender"
            && message.Headers.ContainsKey(MessagingHeaders.MessageId)
            && message.Body != null
            && message.Body.Length > 0);
        messages.Should().ContainSingle(static message =>
            message.Headers!.ContainsKey("tenant")
            && message.Headers["tenant"] == "books"
            && message.Headers[MessagingHeaders.OutboxDestinationKind] == "queue"
            && message.Headers[MessagingHeaders.OutboxDestination] == "processor");
        messages.Should().ContainSingle(static message =>
            message.Headers!.ContainsKey(MessagingHeaders.OutboxDueTime)
            && message.Headers[MessagingHeaders.OutboxDueTime] == "2024-01-01T00:05:00.0000000+00:00");
        messages.Should().ContainSingle(static message =>
            message.Headers![MessagingHeaders.OutboxDestinationKind] == "topic"
            && message.Headers[MessagingHeaders.OutboxDestination] == "publisher-test_event");
    }

    [TestMethod]
    public async Task IncompleteScopeDoesNotPersistAndDirectSendStillWorks()
    {
        var transport = new RecordingTransport();
        using var bus = _createBus(transport);
        var factory = new InMemoryOutboxContextFactory();
        var context = await factory.CreateAsync().ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);
        using (new OutboxScope(context))
            await bus.Send(new TestMessage { Value = "discarded" }).ConfigureAwait(false);
        await context.CommitAsync().ConfigureAwait(false);

        var inspection = await factory.CreateAsync().ConfigureAwait(false);
        await using var __inspection = inspection.ConfigureAwait(false);
        (await inspection.CountAsync().ConfigureAwait(false)).Should().Be(0);

        await bus.Send(new TestMessage { Value = "direct" }).ConfigureAwait(false);
        transport.Sends.Should().ContainSingle();
    }

    [TestMethod]
    public void ReservedProcessorIdentityIsRejectedByDirectBusComposition()
    {
        var transport = new RecordingTransport();
        var action = () => _createBus(transport, "outbox-processor");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*outbox-processor*reserved*");
    }

    [TestMethod]
    public async Task ProcessorDispatchesRawEnvelopeAndDeletesCommittedBatch()
    {
        var factory = new InMemoryOutboxContextFactory();
        var originalHeaders = _outboxHeaders("original-id");
        await _seedAsync(factory, originalHeaders, [1, 2, 3]).ConfigureAwait(false);
        var transport = new RecordingTransport();
        var processor = new MessagingOutboxProcessor(factory, transport, batchSize: 1);
        await using var __processor = processor.ConfigureAwait(false);

        await processor.StartAsync(default).ConfigureAwait(false);
        var sent = await transport.WaitForSendAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (await _countAsync(factory).ConfigureAwait(false) != 0)
            await Task.Delay(10, cts.Token).ConfigureAwait(false);
        await processor.StopAsync(cts.Token).ConfigureAwait(false);

        sent.Destination.Should().Be("processor");
        sent.Headers[MessagingHeaders.MessageId].Should().Be("original-id");
        sent.Headers[MessagingHeaders.SenderIdentity].Should().Be("original-sender");
        sent.Headers.Should().NotContainKey(MessagingHeaders.OutboxDestination);
        sent.Payload.Should().Equal(1, 2, 3);
    }

    [TestMethod]
    public async Task ProcessorFailureLeavesBatchRetryableAndStopsCooperatively()
    {
        var factory = new InMemoryOutboxContextFactory();
        await _seedAsync(factory, _outboxHeaders("retry-id"), [4, 5, 6]).ConfigureAwait(false);
        var transport = new RecordingTransport(fail: true);
        var processor = new MessagingOutboxProcessor(factory, transport, batchSize: 1);
        await using var __processor = processor.ConfigureAwait(false);

        await processor.StartAsync(default).ConfigureAwait(false);
        await transport.WaitForAttemptAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await processor.StopAsync(cts.Token).ConfigureAwait(false);

        (await _countAsync(factory).ConfigureAwait(false)).Should().Be(1);
    }

    [TestMethod]
    public async Task ProcessorSendsDestinationsConcurrentlyAndKeepsOrderWithinEachDestination()
    {
        var factory = new InMemoryOutboxContextFactory();
        await _seedAsync(factory, _queueHeaders("a1", "queue-a"), [1]).ConfigureAwait(false);
        await _seedAsync(factory, _queueHeaders("b1", "queue-b"), [2]).ConfigureAwait(false);
        await _seedAsync(factory, _queueHeaders("a2", "queue-a"), [3]).ConfigureAwait(false);
        await _seedAsync(factory, _queueHeaders("b2", "queue-b"), [4]).ConfigureAwait(false);
        var transport = new DestinationTransport(failingDestination: null, expectedSends: 4);
        var processor = new MessagingOutboxProcessor(factory, transport, batchSize: 10);
        await using var __processor = processor.ConfigureAwait(false);

        await processor.StartAsync(default).ConfigureAwait(false);
        await transport.WaitForExpectedSendsAsync().WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await processor.StopAsync(cts.Token).ConfigureAwait(false);

        transport.MaxInFlight.Should().Be(2);
        transport.MaxInFlightPerDestination.Should().Be(1);
        transport.SentIds("queue-a").Should().Equal("a1", "a2");
        transport.SentIds("queue-b").Should().Equal("b1", "b2");
    }

    [TestMethod]
    [DataRow("mailbox", false)]
    [DataRow("topic", true)]
    public async Task ProcessorRejectsInvalidDestinationHeadersBeforeAnySend(string destinationKind, bool dueTime)
    {
        var factory = new InMemoryOutboxContextFactory();
        await _seedAsync(factory, _queueHeaders("a1", "queue-a"), [1]).ConfigureAwait(false);
        var invalid = _queueHeaders("bad", "queue-b");
        invalid[MessagingHeaders.OutboxDestinationKind] = destinationKind;
        if (dueTime)
            invalid[MessagingHeaders.OutboxDueTime] = "2024-01-01T00:00:00.0000000+00:00";
        await _seedAsync(factory, invalid, [2]).ConfigureAwait(false);
        var transport = new RecordingTransport(fail: false);
        var processor = new MessagingOutboxProcessor(factory, transport, batchSize: 10);
        await using var __processor = processor.ConfigureAwait(false);

        await processor.StartAsync(default).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await processor.StopAsync(cts.Token).ConfigureAwait(false);

        transport.Sends.Should().BeEmpty();
        transport.Publishes.Should().BeEmpty();
        (await _countAsync(factory).ConfigureAwait(false)).Should().Be(2);
    }

    [TestMethod]
    public async Task ProcessorFailureOnOneDestinationCancelsOthersAndDoesNotCommit()
    {
        var factory = new InMemoryOutboxContextFactory();
        await _seedAsync(factory, _queueHeaders("a1", "queue-a"), [1]).ConfigureAwait(false);
        await _seedAsync(factory, _queueHeaders("b1", "queue-b"), [2]).ConfigureAwait(false);
        var transport = new DestinationTransport(failingDestination: "queue-b", expectedSends: 0);
        var processor = new MessagingOutboxProcessor(factory, transport, batchSize: 10);
        await using var __processor = processor.ConfigureAwait(false);

        await processor.StartAsync(default).ConfigureAwait(false);
        await transport.WaitForSiblingCancellationAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await processor.StopAsync(cts.Token).ConfigureAwait(false);

        (await _countAsync(factory).ConfigureAwait(false)).Should().Be(2);
    }

    [TestMethod]
    public async Task ProcessorCompositionRejectsDuplicatesAndResolvesOneHostedService()
    {
        var services = new ServiceCollection();
        var factory = new InMemoryOutboxContextFactory();
        services.AddSingleton<IMessagingTransport>(new RecordingTransport());
        services.AddArkMessagingOutboxProcessor(factory, batchSize: 2);

        var duplicate = () => services.AddArkMessagingOutboxProcessor(factory);
        duplicate.Should().Throw<InvalidOperationException>();
        await using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<MessagingOutboxProcessor>().Should().NotBeNull();
        provider.GetServices<IHostedService>().Should()
            .ContainSingle(static service => service is MessagingOutboxProcessor);
    }

    [TestMethod]
    public async Task StartedProcessorSurvivesServiceProviderDisposal()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessagingTransport>(new RecordingTransport());
        services.AddArkMessagingOutboxProcessor(new InMemoryOutboxContextFactory());
        var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single();
        await hosted.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await hosted.StopAsync(CancellationToken.None).ConfigureAwait(false);

        // The processor is registered as itself and as a hosted service, so the provider disposes it twice.
        var dispose = async () => await provider.DisposeAsync().ConfigureAwait(false);

        await dispose.Should().NotThrowAsync().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task OneScopeStagesTheSendsOfEveryBus()
    {
        var first = new RecordingTransport();
        var second = new RecordingTransport();
        using var firstBus = _createBus(first);
        using var secondBus = _createBus(second, participantIdentity: "other");
        var factory = new InMemoryOutboxContextFactory();
        var context = await factory.CreateAsync().ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);

        using (var scope = new OutboxScope(context))
        {
            await firstBus.Send(new TestMessage { Value = "first" }).ConfigureAwait(false);
            await secondBus.Send(new TestMessage { Value = "second" }).ConfigureAwait(false);
            await scope.CompleteAsync().ConfigureAwait(false);
        }
        await context.CommitAsync().ConfigureAwait(false);

        first.Sends.Should().BeEmpty();
        second.Sends.Should().BeEmpty();
        (await _countAsync(factory).ConfigureAwait(false)).Should().Be(2);
    }

    [TestMethod]
    public async Task DevelopmentPassthroughScopeIsNoOpAndSendsDirectly()
    {
        var transport = new RecordingTransport();
        using var bus = _createBus(transport, outboxMode: MessagingOutboxMode.DevelopmentPassthrough);
        var factory = new InMemoryOutboxContextFactory();
        var context = await factory.CreateAsync().ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);

        using (var scope = new OutboxScope(context))
        {
            await bus.Send(new TestMessage { Value = "direct" }).ConfigureAwait(false);
            transport.Sends.Should().ContainSingle();
            await scope.CompleteAsync().ConfigureAwait(false);
        }
        await context.CommitAsync().ConfigureAwait(false);

        (await _countAsync(factory).ConfigureAwait(false)).Should().Be(0);
    }

    private static MessagingBus _createBus(
        IMessagingTransport transport,
        string participantIdentity = "sender",
        MessagingOutboxMode outboxMode = MessagingOutboxMode.Enqueue)
    {
        var network = new MessagingNetworkOptions(
            typeof(MessagingOutboxTests),
            new MessagingNetworkAttribute
            {
                Requires = MessagingCapabilities.SendReceive
                    | MessagingCapabilities.PubSub
                    | MessagingCapabilities.ScheduledSend,
                MaximumSchedulingDelay = TimeSpan.FromDays(1)
            });
        var codec = new JsonMessagingCodec(new JsonSerializerOptions
        {
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            TypeInfoResolver = OutboxJsonContext.Default
        });
        return new MessagingBus(
            transport,
            network,
            new TestContractRegistry(network.NetworkIdentity),
            new MessagingCodecRegistry([codec]),
            new MessagingPayloadSender(
                new InMemoryMessagingDataBus(
                    NodaTime.SystemClock.Instance,
                    NodaTime.Duration.FromDays(2)),
                network,
                CompressionAlgorithm.None,
                0),
            participantIdentity,
            pipelineProcessor: null,
            outgoingStepTypes: null,
            utcNow: static () => DateTimeOffset.Parse(
                "2024-01-01T00:00:00Z",
                CultureInfo.InvariantCulture),
            outboxMode);
    }

    private static Dictionary<string, string> _outboxHeaders(string messageId)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingHeaders.MessageId] = messageId,
            [MessagingHeaders.MessageType] = "test_message",
            [MessagingHeaders.Network] = "test-network",
            [MessagingHeaders.SenderIdentity] = "original-sender",
            [MessagingHeaders.OutboxDestinationKind] = "queue",
            [MessagingHeaders.OutboxDestination] = "processor",
        };
    }

    private static Dictionary<string, string> _queueHeaders(string messageId, string queue)
    {
        var headers = _outboxHeaders(messageId);
        headers[MessagingHeaders.OutboxDestination] = queue;
        return headers;
    }

    private static async Task _seedAsync(
        IOutboxAsyncContextFactory factory,
        Dictionary<string, string> headers,
        byte[] body)
    {
        var context = await factory.CreateAsync().ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);
        await context.SendAsync([new OutboxMessage { Headers = headers, Body = body }])
            .ConfigureAwait(false);
        await context.CommitAsync().ConfigureAwait(false);
    }

    private static async Task<int> _countAsync(IOutboxAsyncContextFactory factory)
    {
        var context = await factory.CreateAsync().ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);
        return await context.CountAsync().ConfigureAwait(false);
    }

    private sealed class RecordingTransport : IMessagingTransport
    {
        private readonly bool _fail;
        private readonly TaskCompletionSource _attempted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<SentEnvelope> _sent =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RecordingTransport(bool fail = false)
        {
            _fail = fail;
        }

        public MessagingCapabilities Capabilities =>
            MessagingCapabilities.SendReceive
            | MessagingCapabilities.PubSub
            | MessagingCapabilities.ScheduledSend;

        public long MaximumPayloadBytes => long.MaxValue;

        public List<SentEnvelope> Sends { get; } = [];

        public List<SentEnvelope> Publishes { get; } = [];

        public long MeasureNativeHeaders(IReadOnlyDictionary<string, string> headers)
        {
            return 0;
        }

        public async Task SendAsync(
            string queue,
            IReadOnlyDictionary<string, string> headers,
            ReadOnlySequence<byte> payload,
            DateTimeOffset? dueTime,
            CancellationToken ctk)
        {
            ctk.ThrowIfCancellationRequested();
            _attempted.TrySetResult();
            if (_fail)
                throw new InvalidOperationException("Injected transport failure.");
            var sent = new SentEnvelope(queue, headers, payload.ToArray(), dueTime);
            Sends.Add(sent);
            _sent.TrySetResult(sent);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task PublishAsync(
            string topic,
            IReadOnlyDictionary<string, string> headers,
            ReadOnlySequence<byte> payload,
            CancellationToken ctk)
        {
            ctk.ThrowIfCancellationRequested();
            Publishes.Add(new SentEnvelope(topic, headers, payload.ToArray(), null));
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task<SentEnvelope> WaitForSendAsync()
        {
            return await _sent.Task.ConfigureAwait(false);
        }

        public async Task WaitForAttemptAsync()
        {
            await _attempted.Task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Records per-destination order and concurrency. Every send waits until two sends are in flight, so a sequential
    /// processor times out. With a failing destination, that destination fails once two sends are in flight and the
    /// other destination blocks until its token is cancelled. Outbox counts skip peek-locked rows, so the success test
    /// waits on completed sends instead of an empty outbox.
    /// </summary>
    private sealed class DestinationTransport : IMessagingTransport
    {
        private readonly string? _failingDestination;
        private readonly Lock _gate = new();
        private readonly List<(string Destination, string Id)> _sent = [];
        private readonly Dictionary<string, int> _inFlightPerDestination = new(StringComparer.Ordinal);
        private readonly TaskCompletionSource _twoInFlight =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _siblingCancelled =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _expectedSendsCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly int _expectedSends;
        private int _inFlight;
        private int _completedSends;

        public DestinationTransport(string? failingDestination, int expectedSends)
        {
            _failingDestination = failingDestination;
            _expectedSends = expectedSends;
        }

        public MessagingCapabilities Capabilities => MessagingCapabilities.SendReceive;

        public long MaximumPayloadBytes => long.MaxValue;

        public int MaxInFlight { get; private set; }

        public int MaxInFlightPerDestination { get; private set; }

        public long MeasureNativeHeaders(IReadOnlyDictionary<string, string> headers)
        {
            return 0;
        }

        public IReadOnlyList<string> SentIds(string destination)
        {
            lock (_gate)
            {
                return _sent.Where(sent => sent.Destination == destination).Select(static sent => sent.Id).ToList();
            }
        }

        public async Task WaitForSiblingCancellationAsync()
        {
            await _siblingCancelled.Task.ConfigureAwait(false);
        }

        public async Task WaitForExpectedSendsAsync()
        {
            await _expectedSendsCompleted.Task.ConfigureAwait(false);
        }

        public async Task SendAsync(
            string queue,
            IReadOnlyDictionary<string, string> headers,
            ReadOnlySequence<byte> payload,
            DateTimeOffset? dueTime,
            CancellationToken ctk)
        {
            lock (_gate)
            {
                _sent.Add((queue, headers[MessagingHeaders.MessageId]));
                _inFlight++;
                MaxInFlight = Math.Max(MaxInFlight, _inFlight);
                var perDestination = _inFlightPerDestination.GetValueOrDefault(queue) + 1;
                _inFlightPerDestination[queue] = perDestination;
                MaxInFlightPerDestination = Math.Max(MaxInFlightPerDestination, perDestination);
                if (_inFlight >= 2)
                    _twoInFlight.TrySetResult();
            }

            try
            {
                if (_failingDestination is null)
                {
                    await _twoInFlight.Task.WaitAsync(TimeSpan.FromSeconds(5), ctk).ConfigureAwait(false);
                    if (Interlocked.Increment(ref _completedSends) == _expectedSends)
                        _expectedSendsCompleted.TrySetResult();
                }
                else if (queue == _failingDestination)
                {
                    await _twoInFlight.Task.WaitAsync(TimeSpan.FromSeconds(5), ctk).ConfigureAwait(false);
                    throw new InvalidOperationException("Injected transport failure.");
                }
                else
                {
                    try
                    {
                        await Task.Delay(Timeout.Infinite, ctk).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        _siblingCancelled.TrySetResult();
                        throw;
                    }
                }
            }
            finally
            {
                lock (_gate)
                {
                    _inFlight--;
                    _inFlightPerDestination[queue]--;
                }
            }
        }

        public async Task PublishAsync(
            string topic,
            IReadOnlyDictionary<string, string> headers,
            ReadOnlySequence<byte> payload,
            CancellationToken ctk)
        {
            await SendAsync(topic, headers, payload, null, ctk).ConfigureAwait(false);
        }
    }

    private sealed record SentEnvelope(
        string Destination,
        IReadOnlyDictionary<string, string> Headers,
        byte[] Payload,
        DateTimeOffset? DueTime);

    private sealed class TestContractRegistry : IMessagingContractRegistry
    {
        public TestContractRegistry(string networkIdentity)
        {
            NetworkIdentity = networkIdentity;
        }

        public string NetworkIdentity { get; }

        public string GetDestination<T>() where T : class
        {
            return typeof(T) == typeof(TestEvent) ? "publisher-test_event" : "processor";
        }

        public string GetProcessorIdentity<T>() where T : class
        {
            return "processor";
        }

        public string GetPublisherIdentity<T>() where T : class
        {
            return "sender";
        }

        public SerializationProtocol GetWireProtocol<T>() where T : class
        {
            return SerializationProtocol.Json;
        }

        public string GetLogicalName<T>() where T : class
        {
            return typeof(T) == typeof(TestEvent) ? "test_event" : "test_message";
        }
    }

    private sealed class TestMessage
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class TestEvent
    {
        public string Value { get; init; } = string.Empty;
    }

    [JsonSerializable(typeof(TestMessage))]
    [JsonSerializable(typeof(TestEvent))]
    [JsonSourceGenerationOptions(
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true)]
    private sealed partial class OutboxJsonContext : JsonSerializerContext;
}
