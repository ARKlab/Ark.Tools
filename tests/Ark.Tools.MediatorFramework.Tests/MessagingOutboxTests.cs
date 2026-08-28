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
    public async Task EnlistedOperationsPersistValidatedEnvelopesOnCompletion()
    {
        var transport = new RecordingTransport();
        using var bus = _createBus(transport);
        var factory = new InMemoryOutboxContextFactory();
        var context = await factory.CreateAsync().ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);
        using (var scope = ((IBusOutboxEnlistment)bus).Enlist(context))
        {
            await bus.Send(
                new TestMessage { Value = "send" },
                new Dictionary<string, string>(StringComparer.Ordinal) { ["tenant"] = "books" })
                .ConfigureAwait(false);
            await bus.Send(
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
        messages.Should().OnlyContain(message =>
            message.Headers![MessagingHeaders.SenderIdentity] == "sender"
            && message.Headers.ContainsKey(MessagingHeaders.MessageId)
            && message.Body != null
            && message.Body.Length > 0);
        messages.Should().ContainSingle(message =>
            message.Headers!.ContainsKey("tenant")
            && message.Headers["tenant"] == "books"
            && message.Headers[MessagingHeaders.OutboxDestinationKind] == "queue"
            && message.Headers[MessagingHeaders.OutboxDestination] == "processor");
        messages.Should().ContainSingle(message =>
            message.Headers!.ContainsKey(MessagingHeaders.OutboxDueTime)
            && message.Headers[MessagingHeaders.OutboxDueTime] == "2024-01-01T00:05:00.0000000+00:00");
        messages.Should().ContainSingle(message =>
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
        using (((IBusOutboxEnlistment)bus).Enlist(context))
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
            .ContainSingle(service => service is MessagingOutboxProcessor);
    }

    private static MessagingBus _createBus(
        IMessagingTransport transport,
        string participantIdentity = "sender")
    {
        var network = new MessagingNetworkOptions(
            typeof(MessagingOutboxTests),
            new MessagingNetworkAttribute
            {
                Requires = MessagingCapabilities.PubSub | MessagingCapabilities.ScheduledSend,
                MaximumSchedulingDelay = TimeSpan.FromDays(1)
            });
        var codec = new JsonMessagingCodec(new JsonSerializerOptions
        {
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
            utcNow: () => DateTimeOffset.Parse(
                "2024-01-01T00:00:00Z",
                CultureInfo.InvariantCulture));
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
            MessagingCapabilities.PubSub | MessagingCapabilities.ScheduledSend;

        public long? MaximumInlineEnvelopeBytes => null;

        public List<SentEnvelope> Sends { get; } = [];

        public List<SentEnvelope> Publishes { get; } = [];

        public long MeasureNative(
            IReadOnlyDictionary<string, string> headers,
            in ReadOnlySequence<byte> payload)
        {
            return payload.Length;
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
#pragma warning disable VSTHRD003 // The test completion source represents the processor dispatch.
            return await _sent.Task.ConfigureAwait(false);
#pragma warning restore VSTHRD003
        }

        public async Task WaitForAttemptAsync()
        {
#pragma warning disable VSTHRD003 // The test completion source represents the processor attempt.
            await _attempted.Task.ConfigureAwait(false);
#pragma warning restore VSTHRD003
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
    private sealed partial class OutboxJsonContext : JsonSerializerContext;
}
