// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Azure Service Bus implementation of the messaging transport management seam.</summary>
public sealed class ServiceBusTransportManagement : IMessagingTransportManagement
{
    private const string _ownerPrefix = "ark.tools.mediator-framework:";

    private readonly ServiceBusAdministrationClient _administration;

    /// <summary>Creates the management seam over an application-composed administration client.</summary>
    /// <param name="administration">The Service Bus administration client.</param>
    public ServiceBusTransportManagement(ServiceBusAdministrationClient administration)
    {
        _administration = administration ?? throw new ArgumentNullException(nameof(administration));
    }

    /// <inheritdoc />
    public async Task EnsureQueueAsync(
        string queue,
        int maximumDeliveryCount,
        string ownerIdentity,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDeliveryCount, 1);
        ArgumentException.ThrowIfNullOrEmpty(ownerIdentity);
        var options = new CreateQueueOptions(queue)
        {
            MaxDeliveryCount = maximumDeliveryCount,
            UserMetadata = _owner(ownerIdentity)
        };
        try
        {
            _ = await _administration.CreateQueueAsync(options, ctk).ConfigureAwait(false);
        }
        catch (ServiceBusException ex)
            when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            await _reconcileExistingQueueAsync(
                queue,
                maximumDeliveryCount,
                ownerIdentity,
                ctk).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task EnsureTopicAsync(
        string topic,
        string ownerIdentity,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentException.ThrowIfNullOrEmpty(ownerIdentity);
        var options = new CreateTopicOptions(topic)
        {
            UserMetadata = _owner(ownerIdentity)
        };
        try
        {
            _ = await _administration.CreateTopicAsync(options, ctk).ConfigureAwait(false);
        }
        catch (ServiceBusException ex)
            when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // Existing IaC, publisher, or subscriber-owned topics are intentionally unchanged.
        }
    }

    /// <inheritdoc />
    public async Task EnsureSubscriptionAsync(
        MessagingSubscriptionResource subscription,
        CancellationToken ctk)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        var options = new CreateSubscriptionOptions(subscription.Topic, subscription.Name)
        {
            ForwardTo = subscription.ForwardToQueue,
            MaxDeliveryCount = subscription.MaximumDeliveryCount,
            UserMetadata = _owner(subscription.OwnerIdentity)
        };
        try
        {
            _ = await _administration.CreateSubscriptionAsync(options, ctk).ConfigureAwait(false);
        }
        catch (ServiceBusException ex)
            when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            await _reconcileExistingSubscriptionAsync(subscription, ctk).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessagingTransportSubscription>> GetSubscriptionsAsync(
        string topic,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        var subscriptions = new List<MessagingTransportSubscription>();
        try
        {
            await foreach (var existing in _administration.GetSubscriptionsAsync(topic, ctk)
                .WithCancellation(ctk)
                .ConfigureAwait(false))
            {
                subscriptions.Add(new MessagingTransportSubscription(
                    existing.SubscriptionName,
                    _ownerIdentity(existing.UserMetadata)));
            }
        }
        catch (ServiceBusException ex)
            when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            // A known topic may not exist yet when an unrelated participant starts first.
        }

        return subscriptions;
    }

    /// <inheritdoc />
    public async Task DeleteSubscriptionAsync(
        string topic,
        string subscription,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentException.ThrowIfNullOrEmpty(subscription);
        try
        {
            _ = await _administration.DeleteSubscriptionAsync(topic, subscription, ctk)
                .ConfigureAwait(false);
        }
        catch (ServiceBusException ex)
            when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            // Another instance of the same participant already removed it.
        }
    }

    private async Task _reconcileExistingSubscriptionAsync(
        MessagingSubscriptionResource desired,
        CancellationToken ctk)
    {
        var response = await _administration.GetSubscriptionAsync(
            desired.Topic,
            desired.Name,
            ctk).ConfigureAwait(false);
        var existing = response.Value;
        if (existing.RequiresSession || existing.Status != EntityStatus.Active)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Existing subscription '{0}/{1}' is not compatible with PeekLock processing.",
                    desired.Topic,
                    desired.Name));
        }

        var forwardMatches = string.Equals(
            existing.ForwardTo,
            desired.ForwardToQueue,
            StringComparison.Ordinal);
        var deliveryMatches = existing.MaxDeliveryCount == desired.MaximumDeliveryCount;
        if (forwardMatches && deliveryMatches)
            return;

        if (!string.Equals(
            _ownerIdentity(existing.UserMetadata),
            desired.OwnerIdentity,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Existing foreign subscription '{0}/{1}' has incompatible settings.",
                    desired.Topic,
                    desired.Name));
        }

        existing.ForwardTo = desired.ForwardToQueue;
        existing.MaxDeliveryCount = desired.MaximumDeliveryCount;
        _ = await _administration.UpdateSubscriptionAsync(existing, ctk).ConfigureAwait(false);
    }

    private async Task _reconcileExistingQueueAsync(
        string queue,
        int maximumDeliveryCount,
        string ownerIdentity,
        CancellationToken ctk)
    {
        var response = await _administration.GetQueueAsync(queue, ctk).ConfigureAwait(false);
        var existing = response.Value;
        if (existing.RequiresSession || existing.Status != EntityStatus.Active)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Existing queue '{0}' is not compatible with PeekLock processing.",
                    existing.Name));
        }

        if (existing.MaxDeliveryCount == maximumDeliveryCount)
            return;

        if (!string.Equals(
            _ownerIdentity(existing.UserMetadata),
            ownerIdentity,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Existing foreign queue '{0}' has incompatible settings.",
                    existing.Name));
        }

        existing.MaxDeliveryCount = maximumDeliveryCount;
        _ = await _administration.UpdateQueueAsync(existing, ctk).ConfigureAwait(false);
    }

    private static string _owner(string ownerIdentity)
    {
        return _ownerPrefix + ownerIdentity;
    }

    private static string? _ownerIdentity(string? metadata)
    {
        return metadata?.StartsWith(_ownerPrefix, StringComparison.Ordinal) == true
            ? metadata.Substring(_ownerPrefix.Length)
            : null;
    }
}
