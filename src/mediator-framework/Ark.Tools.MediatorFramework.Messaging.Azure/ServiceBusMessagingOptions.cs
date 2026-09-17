// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Deployment-shaping options for Service Bus entities owned by this host.</summary>
/// <remarks>
/// <para>
/// These options describe the shape of the provisioned entity, not the shape of processing. They
/// are settable properties with a parameterless constructor so a deployment can bind them from
/// configuration (<c>IOptions&lt;ServiceBusMessagingOptions&gt;</c>) instead of rebuilding.
/// </para>
/// <para>
/// <see cref="EnablePartitioning"/> is a create-time-only decision: Service Bus cannot partition an
/// existing entity, so the reconciler compares it and fails loudly rather than degrading silently.
/// </para>
/// </remarks>
public sealed class ServiceBusMessagingOptions
{
    /// <summary>The shortest lock duration Service Bus accepts.</summary>
    public static readonly TimeSpan MinimumLockDuration = TimeSpan.FromSeconds(5);

    /// <summary>The longest lock duration Service Bus accepts.</summary>
    public static readonly TimeSpan MaximumLockDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether queues are created partitioned. Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Partitioning buys throughput at a price: no cross-partition transactions or send-batches,
    /// <c>SessionId</c> becomes the partition key, and ordering and duplicate detection hold only
    /// within a partition. It also cannot be changed after the entity is created.
    /// </remarks>
    public bool EnablePartitioning { get; set; }

    /// <summary>Gets or sets the entity lock duration. Defaults to sixty seconds.</summary>
    /// <remarks>Mutable on an existing entity, so the reconciler updates it in place.</remarks>
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Gets or sets the entity size cap in megabytes, or <see langword="null"/> to keep the tier default.</summary>
    public long? MaxSizeInMegabytes { get; set; }

    /// <summary>Gets or sets the message size cap in kilobytes, or <see langword="null"/> to keep the tier default.</summary>
    /// <remarks>Only Premium namespaces accept a value above the Standard 256 KB limit.</remarks>
    public long? MaxMessageSizeInKilobytes { get; set; }

    /// <summary>Validates the options on their own.</summary>
    /// <exception cref="MessagingCompositionException">The options cannot be satisfied.</exception>
    public void Validate()
    {
        if (LockDuration < MinimumLockDuration || LockDuration > MaximumLockDuration)
        {
            throw new MessagingCompositionException(
                MessagingCompositionDiagnostic.TransportOptionsInvalid,
                FormattableString.Invariant(
                    $"LockDuration ({LockDuration}) is outside the Service Bus range of {MinimumLockDuration} to {MaximumLockDuration}."));
        }

        if (MaxSizeInMegabytes is { } size && size <= 0)
        {
            throw new MessagingCompositionException(
                MessagingCompositionDiagnostic.TransportOptionsInvalid,
                FormattableString.Invariant($"MaxSizeInMegabytes ({size}) must be positive."));
        }

        if (MaxMessageSizeInKilobytes is { } messageSize && messageSize <= 0)
        {
            throw new MessagingCompositionException(
                MessagingCompositionDiagnostic.TransportOptionsInvalid,
                FormattableString.Invariant($"MaxMessageSizeInKilobytes ({messageSize}) must be positive."));
        }
    }

    /// <summary>Validates the options against the processing options that will consume the lock.</summary>
    /// <param name="processing">The processing options of the receiver on this entity.</param>
    /// <exception cref="MessagingCompositionException">The combination cannot be satisfied.</exception>
    /// <remarks>
    /// The renewer scans on an interval and renews once the safety margin is reached, so a lock no
    /// longer than both together expires whatever the handler does. Catching that here, at
    /// composition, is the difference between a startup failure and a 3 a.m. redelivery storm.
    /// </remarks>
    public void Validate(MessagingProcessingOptions processing)
    {
        ArgumentNullException.ThrowIfNull(processing);
        Validate();

        var minimum = processing.RenewalSafetyMargin + processing.RenewalScanInterval;
        if (LockDuration > minimum)
            return;

        throw new MessagingCompositionException(
            MessagingCompositionDiagnostic.TransportOptionsInvalid,
            FormattableString.Invariant(
                $"LockDuration ({LockDuration}) is not longer than RenewalSafetyMargin plus RenewalScanInterval ({minimum}); renewal could never run in time. Raise LockDuration or lower the renewal margin."));
    }
}
