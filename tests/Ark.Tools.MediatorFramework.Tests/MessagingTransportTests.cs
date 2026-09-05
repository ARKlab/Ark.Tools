// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Reflection;

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;
using NodaTime.Testing;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the transport contract and in-memory transport semantics.</summary>
[TestClass]
public sealed class MessagingTransportTests : MessagingTransportConformanceTests
{
    /// <summary>Verifies low-level service registration is not part of the public messaging surface.</summary>
    [TestMethod]
    public void LegacyMessagingRegistrationExtensionsAreInternal()
    {
        var publicMethods = typeof(MessagingServiceCollectionExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name)
            .ToArray();

        publicMethods.Should().ContainSingle().Which.Should().Be("AddArkMessagingOutboxProcessor");
    }

    protected override IMessagingTransport CreateTransport()
    {
        return new InMemoryMessagingTransport();
    }

    [TestMethod]
    public async Task InMemoryTransportUsesConfigurableConservativePayloadLimit()
    {
        var transport = new InMemoryMessagingTransport(
            SystemClock.Instance,
            Duration.FromMinutes(1),
            maximumPayloadBytes: 4);

        var action = async () => await transport.SendAsync(
            "queue",
            new Dictionary<string, string>(StringComparer.Ordinal),
            _sequence(new byte[5]),
            null,
            default).ConfigureAwait(false);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(false);
        transport.MaximumPayloadBytes.Should().Be(4);
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
    public async Task AbandonHonorsConfiguredRetryDelay()
    {
        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var transport = new InMemoryMessagingTransport(clock, Duration.FromMinutes(1));
        transport.ConfigureRetry("queue", 3, TimeSpan.FromMinutes(1));
        await transport.SendAsync("queue", new Dictionary<string, string>(StringComparer.Ordinal), _sequence(1), null, default).ConfigureAwait(false);

        var first = await _receiveOnce(transport, "queue").ConfigureAwait(false);
        await first.AbandonAsync(default).ConfigureAwait(false);

        (await transport.ReceiveBatchAsync("queue", 1, TimeSpan.FromMilliseconds(25), default)
            .ConfigureAwait(false)).Should().BeEmpty();

        clock.Advance(Duration.FromMinutes(1));
        var second = await _receiveOnce(transport, "queue").ConfigureAwait(false);
        second.DeliveryCount.Should().Be(2);
        await second.CompleteAsync(default).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task AbandonDeadLettersAtConfiguredMaximumDeliveryCount()
    {
        var transport = new InMemoryMessagingTransport();
        transport.ConfigureRetry("queue", 2, TimeSpan.Zero);
        await transport.SendAsync("queue", new Dictionary<string, string>(StringComparer.Ordinal), _sequence(1), null, default).ConfigureAwait(false);

        var first = await _receiveOnce(transport, "queue").ConfigureAwait(false);
        await first.AbandonAsync(default).ConfigureAwait(false);
        var second = await _receiveOnce(transport, "queue").ConfigureAwait(false);
        await second.AbandonAsync(default).ConfigureAwait(false);

        var deadLetter = transport.GetDeadLetters("queue").Should().ContainSingle().Which;
        deadLetter.DeliveryCount.Should().Be(2);
        deadLetter.Reason.Should().Be("maximum-delivery-count");
    }

    [TestMethod]
    public async Task RetryPolicyDoublesNativeLimitForSecondLevelRetries()
    {
        var transport = new InMemoryMessagingTransport();
        transport.ConfigureRetry("queue", new SecondLevelRetryPolicy());
        await transport.SendAsync(
            "queue",
            new Dictionary<string, string>(StringComparer.Ordinal),
            _sequence(1),
            null,
            default).ConfigureAwait(false);

        for (var deliveryCount = 1; deliveryCount <= 4; deliveryCount++)
        {
            var delivery = await _receiveOnce(transport, "queue").ConfigureAwait(false);
            delivery.DeliveryCount.Should().Be(deliveryCount);
            await delivery.AbandonAsync(default).ConfigureAwait(false);
        }

        transport.GetDeadLetters("queue").Should().ContainSingle()
            .Which.DeliveryCount.Should().Be(4);
    }

    [TestMethod]
    public async Task LockRenewalExtendsInMemoryDelivery()
    {
        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var transport = new InMemoryMessagingTransport(clock, Duration.FromMinutes(1));
        await transport.SendAsync("queue", new Dictionary<string, string>(StringComparer.Ordinal), _sequence(1), null, default).ConfigureAwait(false);

        var delivery = await _receiveOnce(transport, "queue").ConfigureAwait(false);
        clock.Advance(Duration.FromSeconds(45));
        await delivery.RenewLockAsync(default).ConfigureAwait(false);
        clock.Advance(Duration.FromSeconds(45));
        await delivery.CompleteAsync(default).ConfigureAwait(false);

        transport.GetDeadLetters("queue").Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExpiredDeliveryAtMaximumIsDeadLettered()
    {
        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var transport = new InMemoryMessagingTransport(clock, Duration.FromMinutes(1));
        transport.ConfigureRetry("queue", 1, TimeSpan.Zero);
        await transport.SendAsync("queue", new Dictionary<string, string>(StringComparer.Ordinal), _sequence(1), null, default).ConfigureAwait(false);

        _ = await _receiveOnce(transport, "queue").ConfigureAwait(false);
        clock.Advance(Duration.FromMinutes(1));

        (await transport.ReceiveBatchAsync("queue", 1, TimeSpan.Zero, default).ConfigureAwait(false))
            .Should().BeEmpty();
        transport.GetDeadLetters("queue").Should().ContainSingle();
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
        await transport.EnsureSubscriptionAsync(
            new MessagingSubscriptionResource("topic", "one", "queue-one", 1, "one"),
            default).ConfigureAwait(false);
        await transport.EnsureSubscriptionAsync(
            new MessagingSubscriptionResource("topic", "two", "queue-two", 1, "two"),
            default).ConfigureAwait(false);
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
        (await transport.ReceiveBatchAsync("queue", 1, TimeSpan.Zero, default).ConfigureAwait(false))
            .Should().BeEmpty();

        clock.Advance(Duration.FromMinutes(1));
        var delivery = await _receiveOnce(transport, "queue").ConfigureAwait(false);
        await delivery.CompleteAsync(default).ConfigureAwait(false);
    }

    [TestMethod]
    public void NativeMeasurementIncludesHeaderEncoding()
    {
        var transport = new InMemoryMessagingTransport();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["é"] = "値" };

        transport.MeasureNativeHeaders(headers)
            .Should().Be(2 + 3);
    }

    [TestMethod]
    public async Task ServiceBusTransportMeasuresPropertiesAndRejectsOversizedMessages()
    {
#pragma warning disable CA2000 // The transport owns and disposes the client.
        await using var transport = new ServiceBusMessagingTransport(new ServiceBusClient(
            "Endpoint=sb://localhost/;SharedAccessKeyName=test;SharedAccessKey=dGVzdA=="));
#pragma warning restore CA2000
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["é"] = "値" };

        transport.Capabilities.Should().Be(
            MessagingCapabilities.SendReceive
            | MessagingCapabilities.PubSub
            | MessagingCapabilities.ScheduledSend);
        transport.MaximumPayloadBytes.Should().Be(256 * 1024);
        transport.MeasureNativeHeaders(headers).Should().Be(2 + 3 + 8);

        var oversized = new ReadOnlySequence<byte>(new byte[(256 * 1024) + 1]);
        Func<Task> send = async () => await transport
            .SendAsync("queue", new Dictionary<string, string>(StringComparer.Ordinal), oversized, null, default)
            .ConfigureAwait(false);
        await send.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(false);
    }

    [TestMethod]
    public void RegistrationValidatesNetworkCapabilities()
    {
        var network = new MessagingNetworkOptions(
            typeof(MessagingTransportTests),
            new MessagingNetworkAttribute { Requires = MessagingCapabilities.PubSub });
        var services = new ServiceCollection();

        var action = () => services._addArkMessaging(new ReceiveOnlyTransport(), network);
        action.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("PubSub");
        services._addArkMessaging(new InMemoryMessagingTransport(), network);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IMessagingMessageSource>().Should()
            .BeSameAs(provider.GetRequiredService<IMessagingTransport>());
        provider.GetRequiredService<IMessagingTransportManagement>().Should()
            .BeSameAs(provider.GetRequiredService<IMessagingTransport>());
    }

    [TestMethod]
    public void MessagingRegistrationIsIdempotent()
    {
        var services = new ServiceCollection();
        services._addArkMessaging();
        services._addArkInMemoryMessaging();

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IMessagingCodec>().Should().ContainSingle(static codec => codec is JsonMessagingCodec);
        provider.GetServices<IMessagingTransport>().Should().ContainSingle();
    }

    [TestMethod]
    public async Task InMemorySourceBatchesUpToTheRequestedMaximum()
    {
        var transport = new InMemoryMessagingTransport();
        for (byte value = 1; value <= 5; value++)
            await transport.SendAsync("queue", new Dictionary<string, string>(StringComparer.Ordinal), _sequence(value), null, default).ConfigureAwait(false);

        var batch = await transport.ReceiveBatchAsync("queue", 3, TimeSpan.Zero, default).ConfigureAwait(false);

        batch.Should().HaveCount(3);
        batch.Select(static delivery => delivery.Payload.FirstSpan[0]).Should().Equal(1, 2, 3);
        batch.Select(static delivery => delivery.DeliveryId).Should().OnlyHaveUniqueItems();
        foreach (var delivery in batch)
            await delivery.CompleteAsync(default).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task InMemorySourceReportsLockExpiryFromTheInjectedClock()
    {
        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var transport = new InMemoryMessagingTransport(clock, Duration.FromMinutes(1));
        await transport.SendAsync("queue", new Dictionary<string, string>(StringComparer.Ordinal), _sequence(6), null, default).ConfigureAwait(false);

        var delivery = await _receiveOnce(transport, "queue").ConfigureAwait(false);

        delivery.LockedUntil.Should().Be(Instant.FromUtc(2024, 1, 1, 0, 1).ToDateTimeOffset());
        transport.ReceiverCapabilities.NativeLockDuration.Should().Be(TimeSpan.FromMinutes(1));
        await delivery.CompleteAsync(default).ConfigureAwait(false);
    }

    [TestMethod]
    public void SendOnlyTransportIsNotRegisteredAsAMessageSource()
    {
        var services = new ServiceCollection();
        services._addArkMessaging(new ReceiveOnlyTransport());

        using var provider = services.BuildServiceProvider();
        provider.GetService<IMessagingMessageSource>().Should().BeNull();
        provider.GetRequiredService<IMessagingTransport>().Should().BeOfType<ReceiveOnlyTransport>();
    }

    [TestMethod]
    public void CompositionFailuresCarryTheirNamedDiagnostic()
    {
        var exception = new MessagingCompositionException(
            MessagingCompositionDiagnostic.TransportIsNotAMessageSource,
            "no source");

        exception.Diagnostic.Should().Be(MessagingCompositionDiagnostic.TransportIsNotAMessageSource);
        exception.Message.Should().Contain(nameof(MessagingCompositionDiagnostic.TransportIsNotAMessageSource));
    }

    [TestMethod]
    public void ProcessingOptionsRejectImpossibleConcurrencyCombinations()
    {
        var options = new MessagingProcessingOptions
        {
            MaximumConcurrency = 4,
            InitialConcurrency = 4,
            MinimumConcurrency = 4
        };
        options.Validate();

        options.MinimumConcurrency = 1;
        options.InitialConcurrency = 1;
        options.MaximumConcurrency = 1;
        options.MinimumConcurrency = 2;

        var action = options.Validate;
        action.Should().Throw<MessagingCompositionException>()
            .Which.Diagnostic.Should().Be(MessagingCompositionDiagnostic.ProcessingOptionsInvalid);
    }

    private static ReadOnlySequence<byte> _sequence(params byte[] bytes)
    {
        return new ReadOnlySequence<byte>(bytes);
    }

    private static async Task<IMessagingLockedDelivery> _receiveOnce(
        IMessagingMessageSource source,
        string queue)
    {
        return await source.ReceiveOneAsync(queue).ConfigureAwait(false);
    }

    private sealed class ReceiveOnlyTransport : IMessagingTransport
    {
        public MessagingCapabilities Capabilities => MessagingCapabilities.SendReceive;

        public long MaximumPayloadBytes => long.MaxValue;

        public long MeasureNativeHeaders(IReadOnlyDictionary<string, string> headers)
        {
            return 0;
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

    private sealed class SecondLevelRetryPolicy : IMessagingRetryPolicy
    {
        public int MaximumDeliveryCount => 2;

        public bool SecondLevelRetriesEnabled => true;

        public TimeSpan MaximumHandlerDuration => TimeSpan.FromMinutes(1);

        public TimeSpan RetryDelay => TimeSpan.Zero;
    }
}

/// <summary>Reusable conformance checks for locked-delivery transports.</summary>
public abstract class MessagingTransportConformanceTests
{
    /// <summary>Creates the transport under test.</summary>
    protected abstract IMessagingTransport CreateTransport();

    /// <summary>Creates the transport under test as a pull message source.</summary>
    protected IMessagingMessageSource CreateSource()
    {
        return CreateTransport() as IMessagingMessageSource
            ?? throw new InvalidOperationException("The transport under test is not a message source.");
    }

    /// <summary>Gets the queue used for conformance messages.</summary>
    protected virtual string QueueName => "queue";

    /// <summary>Gets an empty queue used for cancellation checks.</summary>
    protected virtual string EmptyQueueName => "empty";

    /// <summary>Gets the capabilities exercised by the conformance checks.</summary>
    protected virtual MessagingCapabilities Capabilities =>
        MessagingCapabilities.SendReceive;

    [TestMethod]
    public async Task CompetingConsumersReceiveEachMessageOnce()
    {
        if (!Capabilities.HasFlag(MessagingCapabilities.SendReceive))
            return;

        var transport = CreateTransport();
        var source = (IMessagingMessageSource)transport;
        await transport.SendAsync(QueueName, new Dictionary<string, string>(StringComparer.Ordinal), _sequence(9), null, default).ConfigureAwait(false);
        var wait = TimeSpan.FromSeconds(2);
        var batches = await Task.WhenAll(
            source.ReceiveBatchAsync(QueueName, 1, wait, default).AsTask(),
            source.ReceiveBatchAsync(QueueName, 1, wait, default).AsTask()).ConfigureAwait(false);

        batches.Sum(static batch => batch.Count).Should().Be(1);
        foreach (var delivery in batches.SelectMany(static batch => batch))
            await delivery.CompleteAsync(default).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ReceiveHonorsCancellation()
    {
        if (!Capabilities.HasFlag(MessagingCapabilities.SendReceive))
            return;

        var source = CreateSource();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);
        Func<Task> action = async () => await source
            .ReceiveBatchAsync(EmptyQueueName, 1, TimeSpan.FromSeconds(30), cts.Token)
            .ConfigureAwait(false);

        await action.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task RepeatedAbandonIncrementsDeliveryCount()
    {
        if (!Capabilities.HasFlag(MessagingCapabilities.SendReceive))
            return;

        var transport = CreateTransport();
        var source = (IMessagingMessageSource)transport;
        await transport.SendAsync(QueueName, new Dictionary<string, string>(StringComparer.Ordinal), _sequence(10), null, default).ConfigureAwait(false);
        for (var expectedCount = 1; expectedCount <= 3; expectedCount++)
        {
            var delivery = await source.ReceiveOneAsync(QueueName).ConfigureAwait(false);
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

    [TestMethod]
    public async Task EmptyQueueReturnsAnEmptyBatchWithinTheWaitWindow()
    {
        if (!Capabilities.HasFlag(MessagingCapabilities.SendReceive))
            return;

        var source = CreateSource();
        var maxWait = TimeSpan.FromMilliseconds(250);
        var started = System.Diagnostics.Stopwatch.StartNew();

        var batch = await source.ReceiveBatchAsync(EmptyQueueName, 1, maxWait, default).ConfigureAwait(false);

        batch.Should().BeEmpty();
        started.Elapsed.Should().BeLessThan(maxWait + TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task ReceiveNeverReturnsMoreThanTheRequestedMaximum()
    {
        if (!Capabilities.HasFlag(MessagingCapabilities.SendReceive))
            return;

        var transport = CreateTransport();
        var source = (IMessagingMessageSource)transport;
        for (byte value = 1; value <= 3; value++)
            await transport.SendAsync(QueueName, new Dictionary<string, string>(StringComparer.Ordinal), _sequence(value), null, default).ConfigureAwait(false);

        var batch = await source.ReceiveBatchAsync(QueueName, 1, TimeSpan.FromSeconds(2), default).ConfigureAwait(false);

        batch.Should().HaveCount(1);
        foreach (var delivery in batch)
            await delivery.CompleteAsync(default).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task DeliveryIdAndLockExpiryArePopulated()
    {
        if (!Capabilities.HasFlag(MessagingCapabilities.SendReceive))
            return;

        var transport = CreateTransport();
        var source = (IMessagingMessageSource)transport;
        await transport.SendAsync(QueueName, new Dictionary<string, string>(StringComparer.Ordinal), _sequence(11), null, default).ConfigureAwait(false);

        var delivery = await source.ReceiveOneAsync(QueueName).ConfigureAwait(false);

        delivery.DeliveryId.Should().NotBeNullOrEmpty();
        if (source.ReceiverCapabilities.NativeLockDuration is not null)
            delivery.LockedUntil.Should().NotBeNull();
        await delivery.CompleteAsync(default).ConfigureAwait(false);
    }

    [TestMethod]
    public void CapabilitiesDeclareAPositiveBatchSize()
    {
        CreateSource().ReceiverCapabilities.MaximumBatchSize.Should().BeGreaterThan(0);
    }
}
