// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.ObjectModel;

using Ark.Tools.MediatorFramework.Messaging;

namespace Ark.Tools.MediatorFramework.AzureFunctions;

/// <summary>Describes one desired Service Bus event subscription.</summary>
public sealed class MessagingFunctionsSubscription
{
    /// <summary>Creates one desired forwarding subscription.</summary>
    /// <param name="topic">The event topic.</param>
    /// <param name="name">The deterministic subscription name.</param>
    /// <param name="forwardToQueue">The participant identity queue.</param>
    public MessagingFunctionsSubscription(string topic, string name, string forwardToQueue)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(forwardToQueue);
        Topic = topic;
        Name = name;
        ForwardToQueue = forwardToQueue;
    }

    /// <summary>Gets the event topic.</summary>
    public string Topic { get; }

    /// <summary>Gets the deterministic subscription name.</summary>
    public string Name { get; }

    /// <summary>Gets the participant identity queue receiving forwarded copies.</summary>
    public string ForwardToQueue { get; }
}

/// <summary>Describes the generated Azure Functions messaging host resources.</summary>
public sealed class MessagingFunctionsManifest
{
    /// <summary>Creates a host manifest without generated runtime composition metadata.</summary>
    /// <param name="participant">The bound participant type.</param>
    /// <param name="network">The participant network type.</param>
    /// <param name="triggerBinding">The selected trigger binding.</param>
    /// <param name="queue">The participant identity queue.</param>
    /// <param name="connectionConfigurationKey">The Functions connection setting name.</param>
    /// <param name="maximumDeliveryCount">The native entity delivery limit.</param>
    /// <param name="maximumHandlerDuration">The maximum handler duration covered by lock renewal.</param>
    /// <param name="subscriptions">The desired forwarding subscriptions.</param>
    /// <param name="incomingSteps">The host-local incoming pipeline steps.</param>
    /// <param name="outgoingSteps">The host-local outgoing pipeline steps.</param>
    /// <param name="retryDelay">The participant retry visibility delay.</param>
    /// <param name="strictStorageQueueHostSettings">Whether Storage Queue setting mismatches fail startup.</param>
    /// <param name="resources">The generated transport-neutral desired resources.</param>
    public MessagingFunctionsManifest(
        Type participant,
        Type network,
        MessagingFunctionsTriggerBinding triggerBinding,
        string queue,
        string connectionConfigurationKey,
        int maximumDeliveryCount,
        TimeSpan maximumHandlerDuration,
        IEnumerable<MessagingFunctionsSubscription> subscriptions,
        IEnumerable<Type> incomingSteps,
        IEnumerable<Type> outgoingSteps,
        TimeSpan? retryDelay = null,
        bool strictStorageQueueHostSettings = false,
        MessagingResourceManifest? resources = null)
        : this(
            participant,
            network,
            descriptor: null,
            triggerBinding,
            queue,
            connectionConfigurationKey,
            maximumDeliveryCount,
            maximumHandlerDuration,
            subscriptions,
            incomingSteps,
            outgoingSteps,
            retryDelay,
            strictStorageQueueHostSettings,
            resources)
    {
    }

    /// <summary>Creates a generated messaging host manifest.</summary>
    /// <param name="participant">The bound participant type.</param>
    /// <param name="network">The participant network type.</param>
    /// <param name="descriptor">The generated participant runtime descriptor.</param>
    /// <param name="triggerBinding">The selected trigger binding.</param>
    /// <param name="queue">The participant identity queue.</param>
    /// <param name="connectionConfigurationKey">The Functions connection setting name.</param>
    /// <param name="maximumDeliveryCount">The native entity delivery limit.</param>
    /// <param name="maximumHandlerDuration">The maximum handler duration covered by lock renewal.</param>
    /// <param name="subscriptions">The desired forwarding subscriptions.</param>
    /// <param name="incomingSteps">The host-local incoming pipeline steps.</param>
    /// <param name="outgoingSteps">The host-local outgoing pipeline steps.</param>
    /// <param name="retryDelay">The participant retry visibility delay.</param>
    /// <param name="strictStorageQueueHostSettings">Whether Storage Queue setting mismatches fail startup.</param>
    /// <param name="resources">The generated transport-neutral desired resources.</param>
    public MessagingFunctionsManifest(
        Type participant,
        Type network,
        MessagingParticipantDescriptor? descriptor,
        MessagingFunctionsTriggerBinding triggerBinding,
        string queue,
        string connectionConfigurationKey,
        int maximumDeliveryCount,
        TimeSpan maximumHandlerDuration,
        IEnumerable<MessagingFunctionsSubscription> subscriptions,
        IEnumerable<Type> incomingSteps,
        IEnumerable<Type> outgoingSteps,
        TimeSpan? retryDelay = null,
        bool strictStorageQueueHostSettings = false,
        MessagingResourceManifest? resources = null)
    {
        Participant = participant ?? throw new ArgumentNullException(nameof(participant));
        Network = network ?? throw new ArgumentNullException(nameof(network));
        Descriptor = descriptor;
        ArgumentException.ThrowIfNullOrEmpty(queue);
        ArgumentException.ThrowIfNullOrEmpty(connectionConfigurationKey);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDeliveryCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumHandlerDuration, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentNullException.ThrowIfNull(incomingSteps);
        ArgumentNullException.ThrowIfNull(outgoingSteps);

        TriggerBinding = triggerBinding;
        Queue = queue;
        ConnectionConfigurationKey = connectionConfigurationKey;
        MaximumDeliveryCount = maximumDeliveryCount;
        MaximumHandlerDuration = maximumHandlerDuration;
        Subscriptions = new ReadOnlyCollection<MessagingFunctionsSubscription>(subscriptions.ToArray());
        IncomingSteps = new ReadOnlyCollection<Type>(incomingSteps.ToArray());
        OutgoingSteps = new ReadOnlyCollection<Type>(outgoingSteps.ToArray());
        RetryDelay = retryDelay ?? TimeSpan.Zero;
        StrictStorageQueueHostSettings = strictStorageQueueHostSettings;
        Resources = resources ?? new MessagingResourceManifest(
            queue,
            queue,
            maximumDeliveryCount,
            Array.Empty<MessagingTopicResource>(),
            Array.Empty<MessagingSubscriptionResource>(),
            Array.Empty<string>(),
            MessagingResourceLifecycle.External);
    }

    /// <summary>Gets the bound participant type.</summary>
    public Type Participant { get; }

    /// <summary>Gets the messaging network type.</summary>
    public Type Network { get; }

    /// <summary>Gets the generated participant runtime descriptor.</summary>
    public MessagingParticipantDescriptor? Descriptor { get; }

    /// <summary>Gets the compile-time trigger binding.</summary>
    public MessagingFunctionsTriggerBinding TriggerBinding { get; }

    /// <summary>Gets the participant identity queue.</summary>
    public string Queue { get; }

    /// <summary>Gets the Functions connection setting name.</summary>
    public string ConnectionConfigurationKey { get; }

    /// <summary>Gets the native entity maximum delivery count.</summary>
    public int MaximumDeliveryCount { get; }

    /// <summary>Gets the maximum handler duration covered by lock renewal.</summary>
    public TimeSpan MaximumHandlerDuration { get; }

    /// <summary>Gets the desired forwarding subscriptions.</summary>
    public IReadOnlyList<MessagingFunctionsSubscription> Subscriptions { get; }

    /// <summary>Gets the host-local incoming pipeline step types.</summary>
    public IReadOnlyList<Type> IncomingSteps { get; }

    /// <summary>Gets the host-local outgoing pipeline step types.</summary>
    public IReadOnlyList<Type> OutgoingSteps { get; }

    /// <summary>Gets the participant retry visibility delay.</summary>
    public TimeSpan RetryDelay { get; }

    /// <summary>Gets whether Storage Queue host-setting mismatches fail startup.</summary>
    public bool StrictStorageQueueHostSettings { get; }

    /// <summary>Gets the generated transport-neutral desired resources.</summary>
    public MessagingResourceManifest Resources { get; }
}
