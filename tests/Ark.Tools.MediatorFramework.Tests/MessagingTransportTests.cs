// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;
using NodaTime.Testing;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the transport contract and in-memory transport semantics.</summary>
[TestClass]
public sealed class MessagingTransportTests
{
    [TestMethod]
    public async Task AbandonRequeuesAndIncrementsDeliveryCount()
    {
        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var transport = new InMemoryMessagingTransport(clock, Duration.FromMinutes(1));
        await transport.SendAsync("queue", new Dictionary<string, string>(), Sequence(1), null, default);

        var first = await ReceiveOnce(transport, "queue");
        first.DeliveryCount.Should().Be(1);
        await first.AbandonAsync(default);

        var second = await ReceiveOnce(transport, "queue");
        second.DeliveryCount.Should().Be(2);
        await second.CompleteAsync(default);
    }

    [TestMethod]
    public async Task ExpiredLockRequeuesAndDeadLetterPreservesMetadata()
    {
        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var transport = new InMemoryMessagingTransport(clock, Duration.FromMinutes(1));
        await transport.SendAsync(
            "queue",
            new Dictionary<string, string> { ["x"] = "y" },
            Sequence(2),
            null,
            default);

        var first = await ReceiveOnce(transport, "queue");
        clock.Advance(Duration.FromMinutes(1));
        var second = await ReceiveOnce(transport, "queue");
        second.DeliveryCount.Should().Be(2);
        await second.DeadLetterAsync("invalid", "bad payload", default);

        var deadLetter = transport.GetDeadLetters("queue").Should().ContainSingle().Which;
        deadLetter.Reason.Should().Be("invalid");
        deadLetter.Description.Should().Be("bad payload");
        deadLetter.Headers["x"].Should().Be("y");
        deadLetter.DeliveryCount.Should().Be(2);
        _ = first;
    }

    [TestMethod]
    public async Task PublishFansOutToEachSubscription()
    {
        var transport = new InMemoryMessagingTransport();
        await transport.EnsureSubscriptionAsync("topic", "one", "queue-one", default);
        await transport.EnsureSubscriptionAsync("topic", "two", "queue-two", default);
        await transport.PublishAsync("topic", new Dictionary<string, string>(), Sequence(3), default);

        var first = await ReceiveOnce(transport, "queue-one");
        var second = await ReceiveOnce(transport, "queue-two");
        first.Payload.ToArray().Should().Equal(3);
        second.Payload.ToArray().Should().Equal(3);
        await first.CompleteAsync(default);
        await second.CompleteAsync(default);
    }

    [TestMethod]
    public async Task ScheduledSendBecomesVisibleAtDueTime()
    {
        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var transport = new InMemoryMessagingTransport(clock, Duration.FromMinutes(1));
        await transport.SendAsync(
            "queue",
            new Dictionary<string, string>(),
            Sequence(4),
            clock.GetCurrentInstant().ToDateTimeOffset().AddMinutes(1),
            default);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var enumerator = transport.ReceiveAsync("queue", cts.Token).GetAsyncEnumerator();
        var move = enumerator.MoveNextAsync().AsTask();
        clock.Advance(Duration.FromMinutes(1));

        (await move).Should().BeTrue();
        await enumerator.Current.CompleteAsync(default);
        await enumerator.DisposeAsync();
    }

    [TestMethod]
    public void NativeMeasurementIncludesHeaderEncoding()
    {
        var transport = new InMemoryMessagingTransport();
        var headers = new Dictionary<string, string> { ["é"] = "値" };

        transport.MeasureNative(headers, Sequence(1, 2))
            .Should().Be(2 + 2 + 3);
    }

    [TestMethod]
    public void RegistrationValidatesNetworkCapabilities()
    {
        var network = new MessagingNetworkOptions(
            typeof(MessagingTransportTests),
            new MessagingNetworkAttribute { Requires = MessagingCapabilities.PubSub });
        using var services = new ServiceCollection();

        services.AddArkMessaging(new ReceiveOnlyTransport(), network);

        var action = () => services.AddArkMessaging(new ReceiveOnlyTransport(), network);
        action.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("PubSub");
    }

    [TestMethod]
    public async Task ReceivePumpInvokesCallback()
    {
        var transport = new InMemoryMessagingTransport();
        await transport.SendAsync("queue", new Dictionary<string, string>(), Sequence(5), null, default);
        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var pump = new MessagingReceivePump(
            transport,
            "queue",
            async (delivery, ctk) =>
            {
                received.SetResult(delivery.Payload.FirstSpan[0]);
                await delivery.CompleteAsync(ctk);
            });

        await pump.StartAsync(default);
        (await received.Task.WaitAsync(TimeSpan.FromSeconds(2))).Should().Be(5);
    }

    private static ReadOnlySequence<byte> Sequence(params byte[] bytes)
    {
        return new ReadOnlySequence<byte>(bytes);
    }

    private static async Task<IMessagingLockedDelivery> ReceiveOnce(
        IMessagingReceiveTransport transport,
        string queue)
    {
        await foreach (var delivery in transport.ReceiveAsync(queue))
            return delivery;

        throw new InvalidOperationException("The receive stream ended without a delivery.");
    }

    private sealed class ReceiveOnlyTransport : IMessagingTransport
    {
        public MessagingCapabilities Capabilities => MessagingCapabilities.Receive;

        public long? MaximumInlineEnvelopeBytes => null;

        public long MeasureNative(
            IReadOnlyDictionary<string, string> headers,
            in ReadOnlySequence<byte> payload)
        {
            return payload.Length;
        }

        public Task SendAsync(
            string queue,
            IReadOnlyDictionary<string, string> headers,
            ReadOnlySequence<byte> payload,
            DateTimeOffset? dueTime,
            CancellationToken ctk)
        {
            return Task.CompletedTask;
        }

        public Task PublishAsync(
            string topic,
            IReadOnlyDictionary<string, string> headers,
            ReadOnlySequence<byte> payload,
            CancellationToken ctk)
        {
            return Task.CompletedTask;
        }
    }
}
