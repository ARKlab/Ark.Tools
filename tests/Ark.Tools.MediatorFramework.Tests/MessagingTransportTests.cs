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
public sealed class MessagingTransportTests : MessagingTransportConformanceTests
{
    protected override IMessagingReceiveTransport CreateTransport()
    {
        return new InMemoryMessagingTransport();
    }

    [TestMethod]
    public async Task AbandonRequeuesAndIncrementsDeliveryCount()
    {
        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var transport = new InMemoryMessagingTransport(clock, Duration.FromMinutes(1));
        await transport.SendAsync("queue", new Dictionary<string, string>(StringComparer.Ordinal), _sequence(1), null, default).ConfigureAwait(false);

        var first = await _receiveOnce(transport, "queue").ConfigureAwait(false);
        first.DeliveryCount.Should().Be(1);
        await first.AbandonAsync(default).ConfigureAwait(false);

        var second = await _receiveOnce(transport, "queue").ConfigureAwait(false);
        second.DeliveryCount.Should().Be(2);
        await second.CompleteAsync(default).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ExpiredLockRequeuesAndDeadLetterPreservesMetadata()
    {
        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var transport = new InMemoryMessagingTransport(clock, Duration.FromMinutes(1));
        await transport.SendAsync(
            "queue",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["x"] = "y" },
            _sequence(2),
            null,
            default).ConfigureAwait(false);

        var first = await _receiveOnce(transport, "queue").ConfigureAwait(false);
        clock.Advance(Duration.FromMinutes(1));
        var second = await _receiveOnce(transport, "queue").ConfigureAwait(false);
        second.DeliveryCount.Should().Be(2);
        await second.DeadLetterAsync("invalid", "bad payload", default).ConfigureAwait(false);

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
        await transport.EnsureSubscriptionAsync("topic", "one", "queue-one", default).ConfigureAwait(false);
        await transport.EnsureSubscriptionAsync("topic", "two", "queue-two", default).ConfigureAwait(false);
        await transport.PublishAsync("topic", new Dictionary<string, string>(StringComparer.Ordinal), _sequence(3), default).ConfigureAwait(false);

        var first = await _receiveOnce(transport, "queue-one").ConfigureAwait(false);
        var second = await _receiveOnce(transport, "queue-two").ConfigureAwait(false);
        first.Payload.ToArray().Should().Equal(3);
        second.Payload.ToArray().Should().Equal(3);
        await first.CompleteAsync(default).ConfigureAwait(false);
        await second.CompleteAsync(default).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ScheduledSendBecomesVisibleAtDueTime()
    {
        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var transport = new InMemoryMessagingTransport(clock, Duration.FromMinutes(1));
        await transport.SendAsync(
            "queue",
            new Dictionary<string, string>(StringComparer.Ordinal),
            _sequence(4),
            clock.GetCurrentInstant().ToDateTimeOffset().AddMinutes(1),
            default).ConfigureAwait(false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var enumerator = transport.ReceiveAsync("queue", cts.Token).GetAsyncEnumerator(cts.Token);
        var move = enumerator.MoveNextAsync().AsTask();
        clock.Advance(Duration.FromMinutes(1));

        (await move.ConfigureAwait(false)).Should().BeTrue();
        await enumerator.Current.CompleteAsync(default).ConfigureAwait(false);
        await enumerator.DisposeAsync().ConfigureAwait(false);
    }

    [TestMethod]
    public void NativeMeasurementIncludesHeaderEncoding()
    {
        var transport = new InMemoryMessagingTransport();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["é"] = "値" };

        transport.MeasureNative(headers, _sequence(1, 2))
            .Should().Be(2 + 2 + 3);
    }

    [TestMethod]
    public void RegistrationValidatesNetworkCapabilities()
    {
        var network = new MessagingNetworkOptions(
            typeof(MessagingTransportTests),
            new MessagingNetworkAttribute { Requires = MessagingCapabilities.PubSub });
        var services = new ServiceCollection();

        var action = () => services.AddArkMessaging(new ReceiveOnlyTransport(), network);
        action.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("PubSub");
        services.AddArkMessaging(new InMemoryMessagingTransport(), network);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IMessagingReceiveTransport>().Should()
            .BeSameAs(provider.GetRequiredService<IMessagingTransport>());
        provider.GetRequiredService<IMessagingTransportManagement>().Should()
            .BeSameAs(provider.GetRequiredService<IMessagingTransport>());
    }

    [TestMethod]
    public void MessagingRegistrationIsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddArkMessaging();
        services.AddArkInMemoryMessaging();

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IMessagingCodec>().Should().ContainSingle(codec => codec is JsonMessagingCodec);
        provider.GetServices<IMessagingTransport>().Should().ContainSingle();
    }

    [TestMethod]
    public async Task ReceivePumpInvokesCallback()
    {
        var transport = new InMemoryMessagingTransport();
        await transport.SendAsync("queue", new Dictionary<string, string>(StringComparer.Ordinal), _sequence(5), null, default).ConfigureAwait(false);
        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pump = new MessagingReceivePump(
            transport,
            "queue",
            async (delivery, ctk) =>
            {
                received.SetResult(delivery.Payload.FirstSpan[0]);
                await delivery.CompleteAsync(ctk).ConfigureAwait(false);
            });

        try
        {
            await pump.StartAsync(default).ConfigureAwait(false);
            (await received.Task.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false)).Should().Be(5);
        }
        finally
        {
            await pump.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static ReadOnlySequence<byte> _sequence(params byte[] bytes)
    {
        return new ReadOnlySequence<byte>(bytes);
    }

    private static async Task<IMessagingLockedDelivery> _receiveOnce(
        IMessagingReceiveTransport transport,
        string queue)
    {
        await foreach (var delivery in transport.ReceiveAsync(queue, CancellationToken.None).ConfigureAwait(false))
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

/// <summary>Reusable conformance checks for locked-delivery transports.</summary>
public abstract class MessagingTransportConformanceTests
{
    /// <summary>Creates the transport under test.</summary>
    protected abstract IMessagingReceiveTransport CreateTransport();

    /// <summary>Gets the capabilities exercised by the conformance checks.</summary>
    protected virtual MessagingCapabilities Capabilities =>
        MessagingCapabilities.Receive;

    [TestMethod]
    public async Task CompetingConsumersReceiveEachMessageOnce()
    {
        if (!Capabilities.HasFlag(MessagingCapabilities.Receive))
            return;

        var transport = CreateTransport();
        await transport.SendAsync("queue", new Dictionary<string, string>(StringComparer.Ordinal), _sequence(9), null, default).ConfigureAwait(false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var first = transport.ReceiveAsync("queue", cts.Token).GetAsyncEnumerator(cts.Token);
        var second = transport.ReceiveAsync("queue", cts.Token).GetAsyncEnumerator(cts.Token);
        var results = await Task.WhenAll(
            _tryMoveNextAsync(first),
            _tryMoveNextAsync(second)).ConfigureAwait(false);

        results.Count(static result => result).Should().Be(1);
        if (results[0])
            await first.Current.CompleteAsync(default).ConfigureAwait(false);
        if (results[1])
            await second.Current.CompleteAsync(default).ConfigureAwait(false);
        await first.DisposeAsync().ConfigureAwait(false);
        await second.DisposeAsync().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ReceiveHonorsCancellation()
    {
        if (!Capabilities.HasFlag(MessagingCapabilities.Receive))
            return;

        var transport = CreateTransport();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var enumerator = transport.ReceiveAsync("empty", cts.Token).GetAsyncEnumerator(cts.Token);
        Func<Task> action = async () => await enumerator.MoveNextAsync().AsTask().ConfigureAwait(false);

        await action.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
        await enumerator.DisposeAsync().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task RepeatedAbandonIncrementsDeliveryCount()
    {
        if (!Capabilities.HasFlag(MessagingCapabilities.Receive))
            return;

        var transport = CreateTransport();
        await transport.SendAsync("queue", new Dictionary<string, string>(StringComparer.Ordinal), _sequence(10), null, default).ConfigureAwait(false);
        for (var expectedCount = 1; expectedCount <= 3; expectedCount++)
        {
            var delivery = await _receiveOnceAsync(transport, "queue").ConfigureAwait(false);
            delivery.DeliveryCount.Should().Be(expectedCount);
            if (expectedCount < 3)
                await delivery.AbandonAsync(default).ConfigureAwait(false);
            else
                await delivery.CompleteAsync(default).ConfigureAwait(false);
        }
    }

    private static ReadOnlySequence<byte> _sequence(params byte[] bytes)
    {
        return new ReadOnlySequence<byte>(bytes);
    }

    private static async Task<bool> _tryMoveNextAsync(IAsyncEnumerator<IMessagingLockedDelivery> enumerator)
    {
        try
        {
            return await enumerator.MoveNextAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task<IMessagingLockedDelivery> _receiveOnceAsync(
        IMessagingReceiveTransport transport,
        string queue)
    {
        await foreach (var delivery in transport.ReceiveAsync(queue, CancellationToken.None).ConfigureAwait(false))
            return delivery;

        throw new InvalidOperationException("The receive stream ended without a delivery.");
    }
}
