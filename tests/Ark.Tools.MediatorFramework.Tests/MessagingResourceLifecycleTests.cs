// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies participant-owned messaging resource reconciliation.</summary>
[TestClass]
public sealed class MessagingResourceLifecycleTests
{
    [TestMethod]
    public async Task ConcurrentParticipantsReconcileOnlyOwnedSubscriptions()
    {
        var transport = new InMemoryMessagingTransport();
        await transport.EnsureSubscriptionAsync(
            _subscription("publisher-current", "obsolete", "consumer-a"),
            default).ConfigureAwait(false);
        await transport.EnsureSubscriptionAsync(
            _subscription("publisher-current", "foreign", "foreign"),
            default).ConfigureAwait(false);
        await transport.EnsureSubscriptionAsync(
            _subscription("publisher-former", "consumer-a", "consumer-a"),
            default).ConfigureAwait(false);
        var consumerA = _manifest(
            "consumer-a",
            ["publisher-current", "publisher-second"],
            ["publisher-current", "publisher-second"],
            ["publisher-current", "publisher-second"]);
        var consumerB = _manifest(
            "consumer-b",
            ["publisher-current"],
            ["publisher-current"],
            ["publisher-current", "publisher-second"]);

        await new MessagingResourceReconciler(transport).ReconcileAsync(consumerB, default)
            .ConfigureAwait(false);
        await Task.WhenAll(
            new MessagingResourceReconciler(transport).ReconcileAsync(consumerA, default),
            new MessagingResourceReconciler(transport).ReconcileAsync(consumerA, default),
            new MessagingResourceReconciler(transport).ReconcileAsync(consumerB, default))
            .ConfigureAwait(false);

        var current = await transport.GetSubscriptionsAsync("publisher-current", default)
            .ConfigureAwait(false);
        current.Select(static subscription => subscription.Name).Should()
            .BeEquivalentTo(["consumer-a", "consumer-b", "foreign"]);
        var second = await transport.GetSubscriptionsAsync("publisher-second", default)
            .ConfigureAwait(false);
        second.Select(static subscription => subscription.Name).Should()
            .BeEquivalentTo(["consumer-a"]);
        var former = await transport.GetSubscriptionsAsync("publisher-former", default)
            .ConfigureAwait(false);
        former.Select(static subscription => subscription.Name).Should()
            .BeEquivalentTo(["consumer-a"]);
    }

    [TestMethod]
    public async Task PartialFailureIsDiagnosableAndRestartable()
    {
        var management = new FailOnceManagement();
        var reconciler = new MessagingResourceReconciler(management);
        var manifest = _manifest(
            "consumer-a",
            ["publisher-current"],
            ["publisher-current"],
            ["publisher-current"]);

        var action = async () => await reconciler.ReconcileAsync(manifest, default)
            .ConfigureAwait(false);
        var failure = await action.Should().ThrowAsync<MessagingResourceManagementException>()
            .ConfigureAwait(false);
        failure.Which.Operation.Should().Be("ensure-subscription");
        failure.Which.Resource.Should().Be("publisher-current/consumer-a");
        failure.Which.InnerException.Should().BeOfType<TimeoutException>();

        await reconciler.ReconcileAsync(manifest, default).ConfigureAwait(false);
        var subscriptions = await management.GetSubscriptionsAsync("publisher-current", default)
            .ConfigureAwait(false);
        subscriptions.Select(static subscription => subscription.Name).Should()
            .BeEquivalentTo(["consumer-a"]);
    }

    [TestMethod]
    public async Task ExternalLifecyclePerformsNoManagementOperations()
    {
        var manifest = new MessagingResourceManifest(
            "consumer-a",
            "consumer-a",
            2,
            Array.Empty<MessagingTopicResource>(),
            Array.Empty<MessagingSubscriptionResource>(),
            Array.Empty<string>(),
            MessagingResourceLifecycle.External);

        await new MessagingResourceReconciler(new ThrowingManagement())
            .ReconcileAsync(manifest, default).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ServiceBusManagementAppliesOwnershipAndDeliverySettings()
    {
        var administration = new RecordingAdministrationClient();
        var management = new ServiceBusTransportManagement(administration);
        var subscription = _subscription(
            "publisher-current",
            "consumer-a",
            "consumer-a");

        await management.EnsureQueueAsync("consumer-a", 4, "consumer-a", default)
            .ConfigureAwait(false);
        await management.EnsureTopicAsync("publisher-current", "publisher", default)
            .ConfigureAwait(false);
        await management.EnsureSubscriptionAsync(subscription, default)
            .ConfigureAwait(false);

        administration.Queue.Should().NotBeNull();
        administration.Queue!.MaxDeliveryCount.Should().Be(4);
        administration.Queue.UserMetadata.Should()
            .Be("ark.tools.mediator-framework:consumer-a");
        administration.Topic.Should().NotBeNull();
        administration.Topic!.UserMetadata.Should()
            .Be("ark.tools.mediator-framework:publisher");
        administration.Subscription.Should().NotBeNull();
        administration.Subscription!.ForwardTo.Should().Be("consumer-a");
        administration.Subscription.MaxDeliveryCount.Should().Be(4);
        administration.Subscription.UserMetadata.Should()
            .Be("ark.tools.mediator-framework:consumer-a");
    }

    [TestMethod]
    public async Task ServiceBusManagementUpdatesExistingDeliverySettings()
    {
        var administration = new ExistingAdministrationClient();
        var management = new ServiceBusTransportManagement(administration);

        await management.EnsureQueueAsync("consumer-a", 4, "consumer-a", default)
            .ConfigureAwait(false);
        await management.EnsureSubscriptionAsync(
            _subscription("publisher-current", "consumer-a", "consumer-a"),
            default).ConfigureAwait(false);

        administration.UpdatedQueue.Should().NotBeNull();
        administration.UpdatedQueue!.MaxDeliveryCount.Should().Be(4);
        administration.UpdatedSubscription.Should().NotBeNull();
        administration.UpdatedSubscription!.MaxDeliveryCount.Should().Be(4);
    }

    private static MessagingResourceManifest _manifest(
        string identity,
        IReadOnlyList<string> desiredTopics,
        IReadOnlyList<string> subscribedTopics,
        IReadOnlyList<string> knownTopics)
    {
        return new MessagingResourceManifest(
            identity,
            identity,
            4,
            desiredTopics.Select(topic => new MessagingTopicResource(topic, "publisher")),
            subscribedTopics.Select(topic => _subscription(topic, identity, identity)),
            knownTopics,
            MessagingResourceLifecycle.CreateIfMissing);
    }

    private static MessagingSubscriptionResource _subscription(
        string topic,
        string name,
        string owner)
    {
        return new MessagingSubscriptionResource(topic, name, owner, 4, owner);
    }

    private sealed class FailOnceManagement : IMessagingTransportManagement
    {
        private readonly InMemoryMessagingTransport _inner = new();
        private int _failures;

        public async Task EnsureQueueAsync(
            string queue,
            int maximumDeliveryCount,
            string ownerIdentity,
            CancellationToken ctk)
        {
            await _inner.EnsureQueueAsync(queue, maximumDeliveryCount, ownerIdentity, ctk)
                .ConfigureAwait(false);
        }

        public async Task EnsureTopicAsync(
            string topic,
            string ownerIdentity,
            CancellationToken ctk)
        {
            await _inner.EnsureTopicAsync(topic, ownerIdentity, ctk).ConfigureAwait(false);
        }

        public async Task EnsureSubscriptionAsync(
            MessagingSubscriptionResource subscription,
            CancellationToken ctk)
        {
            if (Interlocked.Increment(ref _failures) == 1)
                throw new TimeoutException("Transient management failure.");
            await _inner.EnsureSubscriptionAsync(subscription, ctk).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<MessagingTransportSubscription>> GetSubscriptionsAsync(
            string topic,
            CancellationToken ctk)
        {
            return await _inner.GetSubscriptionsAsync(topic, ctk).ConfigureAwait(false);
        }

        public async Task DeleteSubscriptionAsync(
            string topic,
            string subscription,
            CancellationToken ctk)
        {
            await _inner.DeleteSubscriptionAsync(topic, subscription, ctk).ConfigureAwait(false);
        }
    }

    private sealed class ThrowingManagement : IMessagingTransportManagement
    {
        public async Task EnsureQueueAsync(
            string queue,
            int maximumDeliveryCount,
            string ownerIdentity,
            CancellationToken ctk)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            throw new InvalidOperationException("Management should not run.");
        }

        public async Task EnsureTopicAsync(
            string topic,
            string ownerIdentity,
            CancellationToken ctk)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            throw new InvalidOperationException("Management should not run.");
        }

        public async Task EnsureSubscriptionAsync(
            MessagingSubscriptionResource subscription,
            CancellationToken ctk)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            throw new InvalidOperationException("Management should not run.");
        }

        public async Task<IReadOnlyList<MessagingTransportSubscription>> GetSubscriptionsAsync(
            string topic,
            CancellationToken ctk)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            throw new InvalidOperationException("Management should not run.");
        }

        public async Task DeleteSubscriptionAsync(
            string topic,
            string subscription,
            CancellationToken ctk)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            throw new InvalidOperationException("Management should not run.");
        }
    }

    private sealed class RecordingAdministrationClient : ServiceBusAdministrationClient
    {
        public CreateQueueOptions? Queue { get; private set; }

        public CreateTopicOptions? Topic { get; private set; }

        public CreateSubscriptionOptions? Subscription { get; private set; }

        public override async Task<Response<QueueProperties>> CreateQueueAsync(
            CreateQueueOptions options,
            CancellationToken cancellationToken = default)
        {
            Queue = options;
            return await Task.FromResult<Response<QueueProperties>>(null!).ConfigureAwait(false);
        }

        public override async Task<Response<TopicProperties>> CreateTopicAsync(
            CreateTopicOptions options,
            CancellationToken cancellationToken = default)
        {
            Topic = options;
            return await Task.FromResult<Response<TopicProperties>>(null!).ConfigureAwait(false);
        }

        public override async Task<Response<SubscriptionProperties>> CreateSubscriptionAsync(
            CreateSubscriptionOptions options,
            CancellationToken cancellationToken = default)
        {
            Subscription = options;
            return await Task.FromResult<Response<SubscriptionProperties>>(null!).ConfigureAwait(false);
        }
    }

    private sealed class ExistingAdministrationClient : ServiceBusAdministrationClient
    {
        private readonly QueueProperties _queue = ServiceBusModelFactory.QueueProperties(
            "consumer-a",
            lockDuration: TimeSpan.FromMinutes(1),
            maxSizeInMegabytes: 1024,
            requiresSession: false,
            defaultMessageTimeToLive: TimeSpan.FromDays(14),
            autoDeleteOnIdle: TimeSpan.MaxValue,
            duplicateDetectionHistoryTimeWindow: TimeSpan.FromMinutes(10),
            maxDeliveryCount: 2,
            status: EntityStatus.Active,
            userMetadata: "iac");
        private readonly SubscriptionProperties _subscription =
            ServiceBusModelFactory.SubscriptionProperties(
                "publisher-current",
                "consumer-a",
                lockDuration: TimeSpan.FromMinutes(1),
                requiresSession: false,
                defaultMessageTimeToLive: TimeSpan.FromDays(14),
                autoDeleteOnIdle: TimeSpan.MaxValue,
                maxDeliveryCount: 2,
                status: EntityStatus.Active,
                forwardTo: "consumer-a",
                userMetadata: "iac");

        public QueueProperties? UpdatedQueue { get; private set; }

        public SubscriptionProperties? UpdatedSubscription { get; private set; }

        public override async Task<Response<QueueProperties>> CreateQueueAsync(
            CreateQueueOptions options,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            throw new ServiceBusException(
                "Queue exists.",
                ServiceBusFailureReason.MessagingEntityAlreadyExists);
        }

        public override async Task<Response<QueueProperties>> GetQueueAsync(
            string queueName,
            CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(Response.FromValue(_queue, null!)).ConfigureAwait(false);
        }

        public override async Task<Response<QueueProperties>> UpdateQueueAsync(
            QueueProperties queue,
            CancellationToken cancellationToken = default)
        {
            UpdatedQueue = queue;
            return await Task.FromResult(Response.FromValue(queue, null!)).ConfigureAwait(false);
        }

        public override async Task<Response<SubscriptionProperties>> CreateSubscriptionAsync(
            CreateSubscriptionOptions options,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            throw new ServiceBusException(
                "Subscription exists.",
                ServiceBusFailureReason.MessagingEntityAlreadyExists);
        }

        public override async Task<Response<SubscriptionProperties>> GetSubscriptionAsync(
            string topicName,
            string subscriptionName,
            CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(Response.FromValue(_subscription, null!))
                .ConfigureAwait(false);
        }

        public override async Task<Response<SubscriptionProperties>> UpdateSubscriptionAsync(
            SubscriptionProperties subscription,
            CancellationToken cancellationToken = default)
        {
            UpdatedSubscription = subscription;
            return await Task.FromResult(Response.FromValue(subscription, null!))
                .ConfigureAwait(false);
        }
    }
}
