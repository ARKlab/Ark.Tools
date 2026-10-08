// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies resource provisioning of fluent native messaging hosts.</summary>
[TestClass]
public sealed class FluentMessagingResourceManagementTests
{
    private const string _printedTopic = TestPublisher.Identity + "-tests.printed";
    private const string _loopedTopic = TestLoopback.Identity + "-tests.looped";

    /// <summary>A publisher over a transport without management cannot be composed under CreateIfMissing.</summary>
    [TestMethod]
    public void PublisherWithoutManagementSeamFailsComposition()
    {
        var services = new ServiceCollection();

        var act = () => services.ConfigureArkMessaging<TestNetwork>(static b => b.Producer<TestPublisher>(static p => p
            .UseTransport(new NonManagingTransport())
            .UseInMemoryDataBus()));

        act.Should().Throw<InvalidOperationException>().WithMessage("*resource lifecycle management*");
    }

    /// <summary>An explicit seam provisions the topics a producer publishes.</summary>
    [TestMethod]
    public async Task ExplicitManagementSeamProvisionsPublishedTopics()
    {
        var management = new RecordingTransportManagement();

        await _runAsync(b => b.Producer<TestPublisher>(p => p
            .UseTransport(new NonManagingTransport())
            .UseInMemoryDataBus()
            .UseResourceManagement(management))).ConfigureAwait(false);

        management.EnsuredTopics.Should().Equal(_printedTopic);
        management.EnsuredQueues.Should().BeEmpty();
        management.EnsuredSubscriptions.Should().BeEmpty();
    }

    /// <summary>A second management seam is rejected like the other selectors.</summary>
    [TestMethod]
    public void SecondManagementSeamIsRejected()
    {
        var services = new ServiceCollection();
        var management = new RecordingTransportManagement();

        var act = () => services.ConfigureArkMessaging<TestNetwork>(b => b.Producer<TestPublisher>(p => p
            .UseTransport(new NonManagingTransport())
            .UseInMemoryDataBus()
            .UseResourceManagement(management)
            .UseResourceManagement(management)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*already selected*");
    }

    /// <summary>A receiver provisions its identity queue, the subscribed topic and its forwarding subscription.</summary>
    [TestMethod]
    public async Task ExplicitManagementSeamProvisionsReceiverQueueAndSubscriptions()
    {
        var management = new RecordingTransportManagement();

        await _runAsync(b => b.Receiver<TestSubscriber>(r => r
            .UseTransport(new NonManagingTransport())
            .UseInMemoryDataBus()
            .UseResourceManagement(management))).ConfigureAwait(false);

        management.EnsuredQueues.Should().Equal((TestSubscriber.Identity, 6));
        management.EnsuredTopics.Should().Equal(_printedTopic);
        management.EnsuredSubscriptions.Should().ContainSingle(static s =>
            s.Topic == _printedTopic
            && s.Name == TestSubscriber.Identity
            && s.ForwardToQueue == TestSubscriber.Identity
            && s.OwnerIdentity == TestSubscriber.Identity
            && s.MaximumDeliveryCount == 6);
    }

    /// <summary>A receiver deletes its own subscription on a network topic it no longer subscribes to.</summary>
    [TestMethod]
    public async Task ReceiverDeletesItsStaleSubscriptionOnAnotherNetworkTopic()
    {
        var management = new RecordingTransportManagement();
        management.Existing[_loopedTopic] =
        [
            new MessagingTransportSubscription(TestSubscriber.Identity, TestSubscriber.Identity),
            new MessagingTransportSubscription("someone-else", "someone-else"),
        ];

        await _runAsync(b => b.Receiver<TestSubscriber>(r => r
            .UseTransport(new NonManagingTransport())
            .UseInMemoryDataBus()
            .UseResourceManagement(management))).ConfigureAwait(false);

        management.DeletedSubscriptions.Should().Equal((_loopedTopic, TestSubscriber.Identity));
    }

    /// <summary>A participant publishing and subscribing to the same event ensures that topic once.</summary>
    [TestMethod]
    public async Task ParticipantPublishingAndSubscribingSameEventEnsuresTopicOnce()
    {
        var management = new RecordingTransportManagement();

        await _runAsync(b => b.Receiver<TestLoopback>(r => r
            .UseTransport(new NonManagingTransport())
            .UseInMemoryDataBus()
            .UseResourceManagement(management))).ConfigureAwait(false);

        management.EnsuredTopics.Should().Equal(_loopedTopic);
        management.EnsuredSubscriptions.Should().ContainSingle(static s =>
            s.Topic == _loopedTopic && s.Name == TestLoopback.Identity);
    }

    private static async Task _runAsync(Action<MessagingCompositionBuilder<TestNetwork>> configure)
    {
        var services = new ServiceCollection();
        services.Configure<JsonSerializerOptions>(
            static options => options.TypeInfoResolver = new DefaultJsonTypeInfoResolver());
        services.ConfigureArkMessaging(configure);
        var provider = services.BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        {
            var hosted = provider.GetServices<IHostedService>().ToArray();
            try
            {
                foreach (var service in hosted)
                    await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                foreach (var service in hosted.Reverse())
                    await service.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static async Task _noDispatchAsync(
        string logicalName,
        IMessagingPayloadReader payload,
        ICommandProcessor processor,
        IRequestProcessor requestProcessor,
        CancellationToken ctk)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new InvalidOperationException("No message is expected.");
    }

    private static async Task _noDispatchFailedAsync(
        string logicalName,
        IMessagingPayloadReader payload,
        int deliveryCount,
        MessagingExceptionInfo error,
        ICommandProcessor processor,
        CancellationToken ctk)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new InvalidOperationException("No failed message is expected.");
    }

    private static MessagingParticipantDescriptor _descriptor(
        Type participantType,
        MessagingNetworkOptions network,
        IMessagingContractRegistry registry,
        string identity,
        IMessagingRetryPolicy retryPolicy,
        bool receives,
        IEnumerable<MessagingTopicResource> publishedTopics,
        IEnumerable<MessagingTopicResource> subscribedTopics)
    {
        return new MessagingParticipantDescriptor(
            participantType,
            network,
            registry,
            identity,
            new[] { SerializationProtocol.Json },
            retryPolicy,
            CompressionAlgorithm.None,
            0,
            receives,
            receives ? _noDispatchAsync : null,
            retryPolicy.SecondLevelRetriesEnabled ? _noDispatchFailedAsync : null,
            Array.Empty<Type>(),
            publishedTopics,
            subscribedTopics,
            new[] { _loopedTopic, _printedTopic });
    }

    /// <summary>Hand-written equivalent of a generated network declaration.</summary>
    private sealed class TestNetwork : IMessagingNetwork<TestNetwork>
    {
        public static IMessagingContractRegistry Registry { get; } = new TestRegistry(typeof(TestNetwork).FullName!);

        public static MessagingNetworkOptions CreateOptions()
        {
            return new MessagingNetworkOptions(
                typeof(TestNetwork),
                new MessagingNetworkAttribute
                {
                    Members = new[] { typeof(TestPublisher), typeof(TestSubscriber), typeof(TestLoopback) },
                    Requires = MessagingCapabilities.SendReceive | MessagingCapabilities.PubSub,
                    MaximumSchedulingDelay = TimeSpan.Zero,
                });
        }
    }

    /// <summary>Publishes <c>tests.printed</c>.</summary>
    private sealed class TestPublisher : IMessagingParticipant<TestPublisher>
    {
        public const string Identity = "test-publisher";

        public static MessagingParticipantDescriptor CreateDescriptor(
            MessagingNetworkOptions network,
            IMessagingContractRegistry registry)
        {
            return _descriptor(
                typeof(TestPublisher),
                network,
                registry,
                Identity,
                MessagingDefaultRetryPolicy.Instance,
                receives: false,
                [new MessagingTopicResource(_printedTopic, Identity)],
                []);
        }
    }

    /// <summary>Subscribes to <c>tests.printed</c> with second-level retries.</summary>
    private sealed class TestSubscriber : IMessagingParticipant<TestSubscriber>
    {
        public const string Identity = "test-subscriber";

        public static MessagingParticipantDescriptor CreateDescriptor(
            MessagingNetworkOptions network,
            IMessagingContractRegistry registry)
        {
            return _descriptor(
                typeof(TestSubscriber),
                network,
                registry,
                Identity,
                new SecondLevelRetryPolicy(),
                receives: true,
                [],
                [new MessagingTopicResource(_printedTopic, TestPublisher.Identity)]);
        }
    }

    /// <summary>Publishes and subscribes to <c>tests.looped</c>.</summary>
    private sealed class TestLoopback : IMessagingParticipant<TestLoopback>
    {
        public const string Identity = "test-loopback";

        public static MessagingParticipantDescriptor CreateDescriptor(
            MessagingNetworkOptions network,
            IMessagingContractRegistry registry)
        {
            return _descriptor(
                typeof(TestLoopback),
                network,
                registry,
                Identity,
                MessagingDefaultRetryPolicy.Instance,
                receives: true,
                [new MessagingTopicResource(_loopedTopic, Identity)],
                [new MessagingTopicResource(_loopedTopic, Identity)]);
        }
    }

    private sealed class SecondLevelRetryPolicy : IMessagingRetryPolicy
    {
        public int MaximumDeliveryCount => 3;

        public bool SecondLevelRetriesEnabled => true;

        public TimeSpan MaximumHandlerDuration => TimeSpan.FromMinutes(1);

        public TimeSpan RetryDelay => TimeSpan.Zero;
    }

    private sealed class TestRegistry(string networkIdentity) : IMessagingContractRegistry
    {
        public string NetworkIdentity { get; } = networkIdentity;

        public string GetDestination<T>() where T : class => throw new NotSupportedException();

        public string GetProcessorIdentity<T>() where T : class => throw new NotSupportedException();

        public string GetPublisherIdentity<T>() where T : class => throw new NotSupportedException();

        public SerializationProtocol GetWireProtocol<T>() where T : class => SerializationProtocol.Json;

        public string GetLogicalName<T>() where T : class => throw new NotSupportedException();
    }

    /// <summary>A transport and message source that does not manage its own resources.</summary>
    private sealed class NonManagingTransport : IMessagingTransport, IMessagingMessageSource
    {
        private readonly InMemoryMessagingTransport _inner = new();

        public MessagingCapabilities Capabilities => _inner.Capabilities;

        public long MaximumPayloadBytes => _inner.MaximumPayloadBytes;

        public MessagingReceiverCapabilities ReceiverCapabilities => _inner.ReceiverCapabilities;

        public long MeasureNativeHeaders(IReadOnlyDictionary<string, string> headers)
        {
            return _inner.MeasureNativeHeaders(headers);
        }

        public async Task SendAsync(
            string queue,
            IReadOnlyDictionary<string, string> headers,
            ReadOnlySequence<byte> payload,
            DateTimeOffset? dueTime,
            CancellationToken ctk)
        {
            await _inner.SendAsync(queue, headers, payload, dueTime, ctk).ConfigureAwait(false);
        }

        public async Task PublishAsync(
            string topic,
            IReadOnlyDictionary<string, string> headers,
            ReadOnlySequence<byte> payload,
            CancellationToken ctk)
        {
            await _inner.PublishAsync(topic, headers, payload, ctk).ConfigureAwait(false);
        }

        public async ValueTask<IReadOnlyList<IMessagingLockedDelivery>> ReceiveBatchAsync(
            string queue,
            int maxMessages,
            TimeSpan maxWait,
            CancellationToken ctk)
        {
            return await _inner.ReceiveBatchAsync(queue, maxMessages, maxWait, ctk).ConfigureAwait(false);
        }
    }

    /// <summary>Records every management call and returns configured existing subscriptions.</summary>
    private sealed class RecordingTransportManagement : IMessagingTransportManagement
    {
        public List<(string Queue, int MaximumDeliveryCount)> EnsuredQueues { get; } = [];

        public List<string> EnsuredTopics { get; } = [];

        public List<MessagingSubscriptionResource> EnsuredSubscriptions { get; } = [];

        public List<(string Topic, string Subscription)> DeletedSubscriptions { get; } = [];

        public Dictionary<string, IReadOnlyList<MessagingTransportSubscription>> Existing { get; } =
            new(StringComparer.Ordinal);

        public async Task EnsureQueueAsync(
            string queue,
            int maximumDeliveryCount,
            string ownerIdentity,
            CancellationToken ctk)
        {
            EnsuredQueues.Add((queue, maximumDeliveryCount));
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task EnsureTopicAsync(string topic, string ownerIdentity, CancellationToken ctk)
        {
            EnsuredTopics.Add(topic);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task EnsureSubscriptionAsync(
            MessagingSubscriptionResource subscription,
            CancellationToken ctk)
        {
            EnsuredSubscriptions.Add(subscription);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<MessagingTransportSubscription>> GetSubscriptionsAsync(
            string topic,
            CancellationToken ctk)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return Existing.TryGetValue(topic, out var existing) ? existing : [];
        }

        public async Task DeleteSubscriptionAsync(string topic, string subscription, CancellationToken ctk)
        {
            DeletedSubscriptions.Add((topic, subscription));
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
