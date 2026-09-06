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
    private readonly ServiceBusMessagingOptions _options;

    /// <summary>Creates the management seam over an application-composed administration client.</summary>
    /// <param name="administration">The Service Bus administration client.</param>
    /// <param name="options">
    /// The entity-shaping options, or <see langword="null"/> for the defaults. These are declared
    /// once for the transport because they describe the namespace's entities, not a single message.
    /// </param>
    public ServiceBusTransportManagement(
        ServiceBusAdministrationClient administration,
        ServiceBusMessagingOptions? options = null)
    {
        _administration = administration ?? throw new ArgumentNullException(nameof(administration));
        _options = options ?? new ServiceBusMessagingOptions();
        _options.Validate();
    }

    /// <inheritdoc />
    public async Task EnsureQueueAsync(
        string queue,
        int maximumDeliveryCount,
        string ownerIdentity,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        queue = ServiceBusMessagingTransport.ToNativeEntityName(queue);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDeliveryCount, 1);
        ArgumentException.ThrowIfNullOrEmpty(ownerIdentity);
        var options = new CreateQueueOptions(queue)
        {
            MaxDeliveryCount = maximumDeliveryCount,
            UserMetadata = _owner(ownerIdentity),
            EnablePartitioning = _options.EnablePartitioning,
            LockDuration = _options.LockDuration
        };
        if (_options.MaxSizeInMegabytes is { } maxSize)
            options.MaxSizeInMegabytes = (int)maxSize;
        if (_options.MaxMessageSizeInKilobytes is { } maxMessageSize)
            options.MaxMessageSizeInKilobytes = maxMessageSize;
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
        topic = ServiceBusMessagingTransport.ToNativeEntityName(topic);
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
        ArgumentException.ThrowIfNullOrEmpty(subscription.Topic);
        ArgumentException.ThrowIfNullOrEmpty(subscription.Name);
        ArgumentException.ThrowIfNullOrEmpty(subscription.ForwardToQueue);
        var topic = ServiceBusMessagingTransport.ToNativeEntityName(subscription.Topic);
        var name = ServiceBusMessagingTransport.ToNativeEntityName(subscription.Name);
        var forwardToQueue = ServiceBusMessagingTransport.ToNativeEntityName(subscription.ForwardToQueue);
        var options = new CreateSubscriptionOptions(topic, name)
        {
            ForwardTo = forwardToQueue,
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
            await _reconcileExistingSubscriptionAsync(
                new MessagingSubscriptionResource(
                    topic,
                    name,
                    forwardToQueue,
                    subscription.MaximumDeliveryCount,
                    subscription.OwnerIdentity),
                ctk).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessagingTransportSubscription>> GetSubscriptionsAsync(
        string topic,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        topic = ServiceBusMessagingTransport.ToNativeEntityName(topic);
        var subscriptions = new List<MessagingTransportSubscription>();
        try
        {
            await foreach (var existing in _administration.GetSubscriptionsAsync(topic, ctk)
                .WithCancellation(ctk)
                .ConfigureAwait(false))
            {
                subscriptions.Add(new MessagingTransportSubscription(
                    existing.SubscriptionName,
                    _ownerIdentity(existing.UserMetadata) ?? existing.SubscriptionName));
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
        topic = ServiceBusMessagingTransport.ToNativeEntityName(topic);
        subscription = ServiceBusMessagingTransport.ToNativeEntityName(subscription);
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

        var forwardMatches = _forwardMatches(existing.ForwardTo, desired.ForwardToQueue);
        var deliveryMatches = existing.MaxDeliveryCount == desired.MaximumDeliveryCount;
        if (forwardMatches && deliveryMatches)
            return;

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

        if (existing.EnablePartitioning != _options.EnablePartitioning)
        {
            throw new MessagingCompositionException(
                MessagingCompositionDiagnostic.ImmutableEntitySettingMismatch,
                FormattableString.Invariant(
                    $"Queue '{existing.Name}' has EnablePartitioning={existing.EnablePartitioning} but the transport declares EnablePartitioning={_options.EnablePartitioning}. Service Bus fixes partitioning when the entity is created, so the only fix is to delete and recreate the queue (draining or accepting the loss of its messages) or to change the declaration to match. On a Premium namespace partitioning is chosen when the namespace itself is created and cannot be enabled per entity afterwards."));
        }

        var updated = false;
        if (existing.MaxDeliveryCount != maximumDeliveryCount)
        {
            existing.MaxDeliveryCount = maximumDeliveryCount;
            updated = true;
        }

        if (existing.LockDuration != _options.LockDuration)
        {
            existing.LockDuration = _options.LockDuration;
            updated = true;
        }

        if (_options.MaxSizeInMegabytes is { } maxSize && existing.MaxSizeInMegabytes != maxSize)
        {
            existing.MaxSizeInMegabytes = maxSize;
            updated = true;
        }

        if (_options.MaxMessageSizeInKilobytes is { } maxMessageSize
            && existing.MaxMessageSizeInKilobytes != maxMessageSize)
        {
            existing.MaxMessageSizeInKilobytes = maxMessageSize;
            updated = true;
        }

        if (!updated)
            return;

        _ = await _administration.UpdateQueueAsync(existing, ctk).ConfigureAwait(false);
    }

    private static bool _forwardMatches(string? existing, string desired)
    {
        if (string.Equals(existing, desired, StringComparison.Ordinal))
            return true;

        return Uri.TryCreate(existing, UriKind.Absolute, out var uri)
            && string.Equals(
                uri.GetComponents(UriComponents.Path, UriFormat.Unescaped),
                desired,
                StringComparison.Ordinal);
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
