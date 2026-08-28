// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Azure.Messaging.ServiceBus.Administration;
using Azure.Storage.Queues;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies resource reconciliation against the local Azure emulators.</summary>
[TestClass]
[TestCategory("integration")]
public sealed class MessagingResourceLifecycleBoundaryTests
{
    private const string _defaultServiceBusConnectionString = "Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
    private const string _ownerPrefix = "ark.tools.mediator-framework:";
    private const string _storageParticipant = "azm12-reconcile-consumer";
    private const string _serviceBusParticipant = "azm12.reconcile.consumer";
    private const string _currentTopic = "azm12.reconcile.current";
    private const string _formerTopic = "azm12.reconcile.former";
    private const string _foreignSubscription = "azm12.reconcile.foreign";
    [TestMethod]
    public async Task StorageQueueReconcileCreatesIdentityAndPoisonQueues()
    {
        var service = new QueueServiceClient("UseDevelopmentStorage=true");
        var queue = service.GetQueueClient(_storageParticipant);
        var poison = service.GetQueueClient(_storageParticipant + "-poison");
        await queue.DeleteIfExistsAsync().ConfigureAwait(false);
        await poison.DeleteIfExistsAsync().ConfigureAwait(false);
        try
        {
            var manifest = new MessagingResourceManifest(
                _storageParticipant,
                _storageParticipant,
                4,
                Array.Empty<MessagingTopicResource>(),
                Array.Empty<MessagingSubscriptionResource>(),
                Array.Empty<string>(),
                MessagingResourceLifecycle.CreateIfMissing);

            await new MessagingResourceReconciler(new StorageQueueMessagingTransport(service))
                .ReconcileAsync(manifest, default).ConfigureAwait(false);

            (await queue.ExistsAsync().ConfigureAwait(false)).Value.Should().BeTrue();
            (await poison.ExistsAsync().ConfigureAwait(false)).Value.Should().BeTrue();
        }
        finally
        {
            await queue.DeleteIfExistsAsync().ConfigureAwait(false);
            await poison.DeleteIfExistsAsync().ConfigureAwait(false);
        }
    }

    [TestMethod]
    public async Task ServiceBusReconcileUpdatesOwnedResourcesAndPreservesForeignResources()
    {
        var administration = new ServiceBusAdministrationClient(_serviceBusConnectionString());
        await _deleteServiceBusResourcesAsync(administration).ConfigureAwait(false);
        try
        {
            _ = await administration.CreateQueueAsync(
                new CreateQueueOptions(_serviceBusParticipant)
                {
                    MaxDeliveryCount = 2,
                    UserMetadata = _ownerPrefix + _serviceBusParticipant
                }).ConfigureAwait(false);
            _ = await administration.CreateTopicAsync(_currentTopic).ConfigureAwait(false);
            _ = await administration.CreateTopicAsync(_formerTopic).ConfigureAwait(false);
            _ = await administration.CreateSubscriptionAsync(
                new CreateSubscriptionOptions(_currentTopic, _serviceBusParticipant)
                {
                    ForwardTo = _serviceBusParticipant,
                    MaxDeliveryCount = 2,
                    UserMetadata = _ownerPrefix + _serviceBusParticipant
                }).ConfigureAwait(false);
            _ = await administration.CreateSubscriptionAsync(
                new CreateSubscriptionOptions(_currentTopic, _foreignSubscription)
                {
                    ForwardTo = _serviceBusParticipant,
                    MaxDeliveryCount = 2,
                    UserMetadata = "iac"
                }).ConfigureAwait(false);
            _ = await administration.CreateSubscriptionAsync(
                new CreateSubscriptionOptions(_formerTopic, _serviceBusParticipant)
                {
                    ForwardTo = _serviceBusParticipant,
                    MaxDeliveryCount = 2,
                    UserMetadata = _ownerPrefix + _serviceBusParticipant
                }).ConfigureAwait(false);

            var manifest = new MessagingResourceManifest(
                _serviceBusParticipant,
                _serviceBusParticipant,
                4,
                [new MessagingTopicResource(_currentTopic, "azm12.reconcile.publisher")],
                [
                    new MessagingSubscriptionResource(
                        _currentTopic,
                        _serviceBusParticipant,
                        _serviceBusParticipant,
                        4,
                        _serviceBusParticipant)
                ],
                [_currentTopic, _formerTopic],
                MessagingResourceLifecycle.CreateIfMissing);
            var reconciler = new MessagingResourceReconciler(
                new ServiceBusTransportManagement(administration));

            await reconciler.ReconcileAsync(manifest, default).ConfigureAwait(false);
            await reconciler.ReconcileAsync(manifest, default).ConfigureAwait(false);

            var queue = await administration.GetQueueAsync(_serviceBusParticipant).ConfigureAwait(false);
            queue.Value.MaxDeliveryCount.Should().Be(4);
            var subscription = await administration
                .GetSubscriptionAsync(_currentTopic, _serviceBusParticipant).ConfigureAwait(false);
            new Uri(subscription.Value.ForwardTo).AbsolutePath.TrimStart('/').Should()
                .Be(_serviceBusParticipant);
            subscription.Value.MaxDeliveryCount.Should().Be(4);
            (await administration.SubscriptionExistsAsync(
                _currentTopic,
                _foreignSubscription).ConfigureAwait(false)).Value.Should().BeTrue();
            (await administration.SubscriptionExistsAsync(
                _formerTopic,
                _serviceBusParticipant).ConfigureAwait(false)).Value.Should().BeFalse();
            (await administration.TopicExistsAsync(_formerTopic).ConfigureAwait(false))
                .Value.Should().BeTrue();
        }
        finally
        {
            await _deleteServiceBusResourcesAsync(administration).ConfigureAwait(false);
        }
    }

    private static async Task _deleteServiceBusResourcesAsync(
        ServiceBusAdministrationClient administration)
    {
        if ((await administration.TopicExistsAsync(_currentTopic).ConfigureAwait(false)).Value)
            await administration.DeleteTopicAsync(_currentTopic).ConfigureAwait(false);
        if ((await administration.TopicExistsAsync(_formerTopic).ConfigureAwait(false)).Value)
            await administration.DeleteTopicAsync(_formerTopic).ConfigureAwait(false);
        if ((await administration.QueueExistsAsync(_serviceBusParticipant).ConfigureAwait(false)).Value)
            await administration.DeleteQueueAsync(_serviceBusParticipant).ConfigureAwait(false);
    }

    private static string _serviceBusConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ARK_SERVICEBUS_EMULATOR_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        return _defaultServiceBusConnectionString;
    }
}
