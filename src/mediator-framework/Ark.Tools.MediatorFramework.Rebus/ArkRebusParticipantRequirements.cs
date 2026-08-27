// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.ObjectModel;

namespace Ark.Tools.MediatorFramework.Rebus;

/// <summary>Describes runtime infrastructure required by a generated Rebus participant host.</summary>
public sealed class ArkRebusParticipantRequirements
{
    /// <summary>Creates immutable participant requirements.</summary>
    public ArkRebusParticipantRequirements(
        string identity,
        string? inputQueueName,
        IEnumerable<Type> publishedEventTypes,
        IEnumerable<Type> subscribedEventTypes,
        TimeSpan maximumHandlerDuration,
        bool requiresCompression,
        bool requiresDataBus)
    {
        ArgumentException.ThrowIfNullOrEmpty(identity);
        ArgumentNullException.ThrowIfNull(publishedEventTypes);
        ArgumentNullException.ThrowIfNull(subscribedEventTypes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumHandlerDuration, TimeSpan.Zero);

        Identity = identity;
        InputQueueName = inputQueueName;
        PublishedEventTypes = new ReadOnlyCollection<Type>(publishedEventTypes.ToArray());
        SubscribedEventTypes = new ReadOnlyCollection<Type>(subscribedEventTypes.ToArray());
        MaximumHandlerDuration = maximumHandlerDuration;
        RequiresCompression = requiresCompression;
        RequiresDataBus = requiresDataBus;
    }

    /// <summary>Gets the participant identity.</summary>
    public string Identity { get; }

    /// <summary>Gets the input queue name, or <see langword="null"/> for a non-receiving host.</summary>
    public string? InputQueueName { get; }

    /// <summary>Gets the published event contract types.</summary>
    public IReadOnlyList<Type> PublishedEventTypes { get; }

    /// <summary>Gets the subscribed event contract types.</summary>
    public IReadOnlyList<Type> SubscribedEventTypes { get; }

    /// <summary>Gets the maximum handler duration.</summary>
    public TimeSpan MaximumHandlerDuration { get; }

    /// <summary>Gets whether runtime compression configuration is required.</summary>
    public bool RequiresCompression { get; }

    /// <summary>Gets whether runtime DataBus configuration is required.</summary>
    public bool RequiresDataBus { get; }

    /// <summary>Validates explicit provider-specific runtime acknowledgements.</summary>
    /// <param name="compressionConfigured">Whether Rebus compression was explicitly configured.</param>
    /// <param name="dataBusConfigured">Whether a Rebus DataBus provider was explicitly configured.</param>
    public void Validate(bool compressionConfigured, bool dataBusConfigured)
    {
        if (RequiresCompression && !compressionConfigured)
            throw new InvalidOperationException("The participant requires explicit Rebus compression configuration.");
        if (RequiresDataBus && !dataBusConfigured)
            throw new InvalidOperationException("The participant requires explicit Rebus DataBus configuration.");
    }
}
