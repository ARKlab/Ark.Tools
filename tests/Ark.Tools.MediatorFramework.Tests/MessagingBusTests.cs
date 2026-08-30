// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using NodaTime;
using NodaTime.Testing;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the restricted transport-neutral messaging bus.</summary>
[TestClass]
public sealed partial class MessagingBusTests
{
    [TestMethod]
    public async Task SendRoutesAndSerializesWithApplicationHeaders()
    {
        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var transport = new InMemoryMessagingTransport(clock, Duration.FromMinutes(1));
        using var bus = _createBus(
            transport,
            MessagingCapabilities.SendReceive | MessagingCapabilities.PubSub | MessagingCapabilities.ScheduledSend,
            clock);

        await bus.Send(
            new TestMessage { Value = "Ada" },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["tenant"] = "books" }).ConfigureAwait(false);

        var delivery = await _receiveOnce(transport, "processor").ConfigureAwait(false);
        delivery.Headers[MessagingHeaders.MessageType].Should().Be("test_message");
        delivery.Headers[MessagingHeaders.SenderIdentity].Should().Be("sender");
        delivery.Headers["tenant"].Should().Be("books");
        var codec = new JsonMessagingCodec(new JsonSerializerOptions
        {
            TypeInfoResolver = TestJsonContext.Default
        });
        codec.Deserialize<TestMessage>(delivery.Payload).Value.Should().Be("Ada");
        await delivery.CompleteAsync(default).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task PublishRoutesToPublisherTopicAndRejectsOtherParticipants()
    {
        var transport = new InMemoryMessagingTransport();
        await transport.EnsureSubscriptionAsync(
            new MessagingSubscriptionResource(
                "publisher-test_event",
                "subscription",
                "subscriber",
                1,
                "subscriber"),
            default).ConfigureAwait(false);
        using var bus = _createBus(
            transport,
            MessagingCapabilities.SendReceive | MessagingCapabilities.PubSub,
            null,
            "publisher");

        await bus.Publish(new TestEvent { Value = "published" }).ConfigureAwait(false);

        var delivery = await _receiveOnce(transport, "subscriber").ConfigureAwait(false);
        delivery.Headers[MessagingHeaders.SenderIdentity].Should().Be("publisher");
        await delivery.CompleteAsync(default).ConfigureAwait(false);

        using var otherBus = _createBus(
            transport,
            MessagingCapabilities.SendReceive | MessagingCapabilities.PubSub,
            null,
            "sender");
        Func<Task> action = () => otherBus.Publish(new TestEvent { Value = "rejected" });
        await action.Should().ThrowAsync<NotSupportedException>().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task DelayedSendRequiresCapabilityAndHonorsDueTime()
    {
        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var transport = new InMemoryMessagingTransport(clock, Duration.FromMinutes(1));
        using var bus = _createBus(transport, MessagingCapabilities.SendReceive | MessagingCapabilities.ScheduledSend, clock);

        await bus.Defer(new TestMessage { Value = "scheduled" }, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var enumerator = transport.ReceiveAsync("processor", cts.Token).GetAsyncEnumerator(cts.Token);
        var move = enumerator.MoveNextAsync().AsTask();
        await Task.Delay(1, cts.Token).ConfigureAwait(false);
        move.IsCompleted.Should().BeFalse();
        clock.Advance(Duration.FromMinutes(1));
        (await move.ConfigureAwait(false)).Should().BeTrue();
        await enumerator.Current.CompleteAsync(default).ConfigureAwait(false);
        await enumerator.DisposeAsync().ConfigureAwait(false);

        using var noSchedule = _createBus(transport, MessagingCapabilities.SendReceive, clock);
        Func<Task> action = () => noSchedule.Defer(new TestMessage(), TimeSpan.Zero, cancellationToken: cts.Token);
        await action.Should().ThrowAsync<NotSupportedException>().WithMessage("*ScheduledSend*")
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ReservedHeadersAndUnwiredRoutesFailExplicitly()
    {
        var transport = new InMemoryMessagingTransport();
        using var bus = _createBus(transport, MessagingCapabilities.SendReceive);

        Func<Task> reserved = () => bus.Send(
            new TestMessage(),
            new Dictionary<string, string>(StringComparer.Ordinal) { [MessagingHeaders.Network] = "spoofed" });
        await reserved.Should().ThrowAsync<ArgumentException>().ConfigureAwait(false);

        Func<Task> unwired = () => bus.Send(new UnwiredMessage());
        await unwired.Should().ThrowAsync<MessagingContractNotInNetworkException>().ConfigureAwait(false);
    }

    [TestMethod]
    public void BusDoesNotExposeReceiveOrReplyOperations()
    {
        typeof(IBus).GetMethods().Select(method => method.Name).Should()
            .BeEquivalentTo("Send", "Defer", "Defer", "Publish");
    }

    private static MessagingBus _createBus(
        IMessagingTransport transport,
        MessagingCapabilities capabilities,
        FakeClock? clock = null,
        string participantIdentity = "sender")
    {
        var network = new MessagingNetworkOptions(
            typeof(MessagingBusTests),
            new MessagingNetworkAttribute
            {
                Requires = capabilities,
                MaximumSchedulingDelay = TimeSpan.FromDays(1)
            });
        var dataBus = new InMemoryMessagingDataBus(
            clock ?? new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0)),
            Duration.FromHours(1));
        var codec = new JsonMessagingCodec(new JsonSerializerOptions
        {
            TypeInfoResolver = TestJsonContext.Default
        });
        var registry = new TestContractRegistry(network.NetworkIdentity);
        return new MessagingBus(
            transport,
            network,
            registry,
            new MessagingCodecRegistry([codec]),
            new MessagingPayloadSender(dataBus, network, CompressionAlgorithm.None, 0),
            participantIdentity,
            utcNow: () => (clock ?? new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0)))
                .GetCurrentInstant().ToDateTimeOffset());
    }

    private sealed class TestContractRegistry : IMessagingContractRegistry
    {
        public TestContractRegistry(string networkIdentity)
        {
            NetworkIdentity = networkIdentity;
        }

        public string NetworkIdentity { get; }

        public string GetDestination<T>() where T : class
        {
            return typeof(T) == typeof(TestMessage)
                ? "processor"
                : typeof(T) == typeof(TestEvent)
                    ? "publisher-test_event"
                    : throw new MessagingContractNotInNetworkException(typeof(T), NetworkIdentity);
        }

        public string GetProcessorIdentity<T>() where T : class
        {
            return typeof(T) == typeof(TestMessage)
                ? "processor"
                : throw new MessagingContractNotInNetworkException(typeof(T), NetworkIdentity);
        }

        public string GetPublisherIdentity<T>() where T : class
        {
            return typeof(T) == typeof(TestEvent)
                ? "publisher"
                : throw new MessagingContractNotInNetworkException(typeof(T), NetworkIdentity);
        }

        public SerializationProtocol GetWireProtocol<T>() where T : class
        {
            return SerializationProtocol.Json;
        }

        public string GetLogicalName<T>() where T : class
        {
            return typeof(T) == typeof(TestMessage)
                ? "test_message"
                : typeof(T) == typeof(TestEvent)
                    ? "test_event"
                    : throw new MessagingContractNotInNetworkException(typeof(T), NetworkIdentity);
        }
    }

    private static async Task<IMessagingLockedDelivery> _receiveOnce(
        IMessagingReceiveTransport transport,
        string queue)
    {
        await foreach (var delivery in transport.ReceiveAsync(queue, CancellationToken.None).ConfigureAwait(false))
            return delivery;
        throw new InvalidOperationException("The receive stream ended without a delivery.");
    }

    private sealed class TestMessage
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class TestEvent
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class UnwiredMessage
    {
    }

    [JsonSerializable(typeof(TestMessage))]
    [JsonSerializable(typeof(TestEvent))]
    private sealed partial class TestJsonContext : JsonSerializerContext
    {
    }
}
