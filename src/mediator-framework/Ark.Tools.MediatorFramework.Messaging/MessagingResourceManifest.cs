// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.ObjectModel;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Describes one desired event topic.</summary>
public sealed class MessagingTopicResource
{
    /// <summary>Creates a desired event topic.</summary>
    /// <param name="name">The topic name.</param>
    /// <param name="ownerIdentity">The publishing participant identity.</param>
    public MessagingTopicResource(string name, string ownerIdentity)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(ownerIdentity);
        Name = name;
        OwnerIdentity = ownerIdentity;
    }

    /// <summary>Gets the topic name.</summary>
    public string Name { get; }

    /// <summary>Gets the publishing participant identity.</summary>
    public string OwnerIdentity { get; }
}

/// <summary>Describes one desired forwarding subscription.</summary>
public sealed class MessagingSubscriptionResource
{
    /// <summary>Creates a desired forwarding subscription.</summary>
    /// <param name="topic">The event topic.</param>
    /// <param name="name">The deterministic subscription name.</param>
    /// <param name="forwardToQueue">The participant identity queue.</param>
    /// <param name="maximumDeliveryCount">The native maximum delivery count.</param>
    /// <param name="ownerIdentity">The subscribing participant identity.</param>
    public MessagingSubscriptionResource(
        string topic,
        string name,
        string forwardToQueue,
        int maximumDeliveryCount,
        string ownerIdentity)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(forwardToQueue);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDeliveryCount, 1);
        ArgumentException.ThrowIfNullOrEmpty(ownerIdentity);
        Topic = topic;
        Name = name;
        ForwardToQueue = forwardToQueue;
        MaximumDeliveryCount = maximumDeliveryCount;
        OwnerIdentity = ownerIdentity;
    }

    /// <summary>Gets the event topic.</summary>
    public string Topic { get; }

    /// <summary>Gets the deterministic subscription name.</summary>
    public string Name { get; }

    /// <summary>Gets the participant identity queue receiving forwarded copies.</summary>
    public string ForwardToQueue { get; }

    /// <summary>Gets the native maximum delivery count.</summary>
    public int MaximumDeliveryCount { get; }

    /// <summary>Gets the subscribing participant identity.</summary>
    public string OwnerIdentity { get; }
}

/// <summary>Describes one existing transport subscription.</summary>
public sealed class MessagingTransportSubscription
{
    /// <summary>Creates an existing transport subscription descriptor.</summary>
    /// <param name="name">The subscription name.</param>
    /// <param name="ownerIdentity">The framework owner identity, if present.</param>
    public MessagingTransportSubscription(string name, string? ownerIdentity)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        OwnerIdentity = ownerIdentity;
    }

    /// <summary>Gets the subscription name.</summary>
    public string Name { get; }

    /// <summary>Gets the framework owner identity, if present.</summary>
    public string? OwnerIdentity { get; }
}

/// <summary>Describes the generated resources desired by one messaging participant.</summary>
public sealed class MessagingResourceManifest
{
    /// <summary>Creates a generated desired-resource manifest.</summary>
    /// <param name="participantIdentity">The participant identity.</param>
    /// <param name="identityQueue">The consumer identity queue, or <see langword="null"/> for a producer-only participant.</param>
    /// <param name="maximumDeliveryCount">The native maximum delivery count.</param>
    /// <param name="topics">Topics published by or subscribed to by this participant.</param>
    /// <param name="subscriptions">Forwarding subscriptions desired by this participant.</param>
    /// <param name="knownNetworkTopics">All current event topics in the network.</param>
    /// <param name="lifecycle">The resource lifecycle policy.</param>
    public MessagingResourceManifest(
        string participantIdentity,
        string? identityQueue,
        int maximumDeliveryCount,
        IEnumerable<MessagingTopicResource> topics,
        IEnumerable<MessagingSubscriptionResource> subscriptions,
        IEnumerable<string> knownNetworkTopics,
        MessagingResourceLifecycle lifecycle)
    {
        ArgumentException.ThrowIfNullOrEmpty(participantIdentity);
        if (identityQueue is not null)
            ArgumentException.ThrowIfNullOrEmpty(identityQueue);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDeliveryCount, 1);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentNullException.ThrowIfNull(knownNetworkTopics);

        ParticipantIdentity = participantIdentity;
        IdentityQueue = identityQueue;
        MaximumDeliveryCount = maximumDeliveryCount;
        Topics = new ReadOnlyCollection<MessagingTopicResource>(topics.ToArray());
        Subscriptions = new ReadOnlyCollection<MessagingSubscriptionResource>(subscriptions.ToArray());
        KnownNetworkTopics = new ReadOnlyCollection<string>(knownNetworkTopics.ToArray());
        Lifecycle = lifecycle;
        _validate();
    }

    /// <summary>Gets the participant identity.</summary>
    public string ParticipantIdentity { get; }

    /// <summary>Gets the consumer identity queue, or <see langword="null"/> for a producer-only participant.</summary>
    public string? IdentityQueue { get; }

    /// <summary>Gets the native maximum delivery count.</summary>
    public int MaximumDeliveryCount { get; }

    /// <summary>Gets topics published by or subscribed to by this participant.</summary>
    public IReadOnlyList<MessagingTopicResource> Topics { get; }

    /// <summary>Gets forwarding subscriptions desired by this participant.</summary>
    public IReadOnlyList<MessagingSubscriptionResource> Subscriptions { get; }

    /// <summary>Gets all current event topics in the network.</summary>
    public IReadOnlyList<string> KnownNetworkTopics { get; }

    /// <summary>Gets the resource lifecycle policy.</summary>
    public MessagingResourceLifecycle Lifecycle { get; }

    private void _validate()
    {
        if (!Enum.IsDefined(Lifecycle))
            throw new ArgumentOutOfRangeException(nameof(Lifecycle));
        if (Subscriptions.Count > 0 && IdentityQueue is null)
            throw new ArgumentException(
                "A participant with subscriptions must have an identity queue.",
                nameof(IdentityQueue));
        if (Subscriptions.Any(subscription =>
            !string.Equals(subscription.ForwardToQueue, IdentityQueue, StringComparison.Ordinal)
            || !string.Equals(subscription.OwnerIdentity, ParticipantIdentity, StringComparison.Ordinal)
            || subscription.MaximumDeliveryCount != MaximumDeliveryCount))
        {
            throw new ArgumentException(
                "Every subscription must use the manifest participant queue, owner, and delivery count.",
                nameof(Subscriptions));
        }
        if (Topics.Select(static topic => topic.Name).Distinct(StringComparer.Ordinal).Count() != Topics.Count
            || Subscriptions.Select(static subscription => (subscription.Topic, subscription.Name))
                .Distinct().Count() != Subscriptions.Count
            || KnownNetworkTopics.Distinct(StringComparer.Ordinal).Count() != KnownNetworkTopics.Count)
        {
            throw new ArgumentException("Messaging resource names must be unique.");
        }
        if (Topics.Any(topic => !KnownNetworkTopics.Contains(topic.Name, StringComparer.Ordinal))
            || Subscriptions.Any(subscription =>
                !KnownNetworkTopics.Contains(subscription.Topic, StringComparer.Ordinal)))
        {
            throw new ArgumentException("Desired topics and subscriptions must belong to the network.");
        }
    }
}
