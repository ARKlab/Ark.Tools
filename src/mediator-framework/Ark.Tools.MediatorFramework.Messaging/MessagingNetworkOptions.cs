// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.ObjectModel;
using System.Globalization;

namespace Ark.MediatorFramework.Messaging;

/// <summary>Immutable, resolved configuration for a messaging network.</summary>
public sealed class MessagingNetworkOptions
{
    /// <summary>Creates options from a network declaration and its identity type.</summary>
    public MessagingNetworkOptions(Type networkType, MessagingNetworkAttribute declaration)
    {
        ArgumentNullException.ThrowIfNull(networkType);
        ArgumentNullException.ThrowIfNull(declaration);

        NetworkType = networkType;
        NetworkIdentity = networkType.FullName ?? networkType.Name;
        Members = new ReadOnlyCollection<Type>((declaration.Members ?? Array.Empty<Type>()).ToArray());
        Requires = declaration.Requires;
        MaximumTransportPayloadBytes = declaration.MaximumTransportPayloadBytes;
        MaximumDecompressedPayloadBytes = declaration.MaximumDecompressedPayloadBytes;
        DataBusOffloadThresholdBytes = declaration.DataBusOffloadThresholdBytes;
        DataBusMaximumAttachmentBytes = declaration.DataBusMaximumAttachmentBytes;
        MaximumSchedulingDelay = declaration.MaximumSchedulingDelay;
        ResourceLifecycle = declaration.ResourceLifecycle;
        ConnectionConfigurationKey = declaration.ConnectionConfigurationKey;
        ManagedIdentityConfigurationKey = declaration.ManagedIdentityConfigurationKey;
    }

    /// <summary>Gets the network declaration type.</summary>
    public Type NetworkType { get; }

    /// <summary>Gets the deterministic network identity.</summary>
    public string NetworkIdentity { get; }

    /// <summary>Gets the opaque participant member list.</summary>
    public IReadOnlyList<Type> Members { get; }

    /// <summary>Gets the capabilities required by the network.</summary>
    public MessagingCapabilities Requires { get; }

    /// <summary>Gets the maximum transport payload size in bytes.</summary>
    public int MaximumTransportPayloadBytes { get; }

    /// <summary>Gets the maximum decompressed payload size in bytes.</summary>
    public int MaximumDecompressedPayloadBytes { get; }

    /// <summary>Gets the DataBus offload threshold in bytes.</summary>
    public int DataBusOffloadThresholdBytes { get; }

    /// <summary>Gets the maximum DataBus attachment size in bytes.</summary>
    public int DataBusMaximumAttachmentBytes { get; }

    /// <summary>Gets the maximum scheduled-send delay.</summary>
    public TimeSpan MaximumSchedulingDelay { get; }

    /// <summary>Gets the resource lifecycle policy.</summary>
    public MessagingResourceLifecycle ResourceLifecycle { get; }

    /// <summary>Gets the transport connection configuration key.</summary>
    public string? ConnectionConfigurationKey { get; }

    /// <summary>Gets the managed identity configuration key.</summary>
    public string? ManagedIdentityConfigurationKey { get; }

    /// <summary>Validates that a composed transport supports every required capability.</summary>
    public void Validate(MessagingCapabilities transportCapabilities)
    {
        var missing = Requires & ~transportCapabilities;
        if (missing != MessagingCapabilities.None)
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Network '{0}' requires unsupported capability '{1}'.",
                    NetworkIdentity,
                    missing));
    }
}
