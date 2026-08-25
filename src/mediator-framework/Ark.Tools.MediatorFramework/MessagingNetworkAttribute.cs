// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Declares a transport-neutral messaging network.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MessagingNetworkAttribute : Attribute
{
    private TimeSpan _maximumSchedulingDelay = TimeSpan.FromDays(7);

    /// <summary>Gets or sets the participant types belonging to the network.</summary>
    public Type[] Members { get; set; } = Array.Empty<Type>();

    /// <summary>Gets or sets the optional capabilities required by the network.</summary>
    public MessagingCapabilities Requires { get; set; }

    /// <summary>Gets or sets the maximum transport payload size in bytes.</summary>
    public int MaximumTransportPayloadBytes { get; set; } = 240_000;

    /// <summary>Gets or sets the maximum decompressed payload size in bytes.</summary>
    public int MaximumDecompressedPayloadBytes { get; set; } = 1_000_000;

    /// <summary>Gets or sets the size at which payloads are offloaded to DataBus.</summary>
    public int DataBusOffloadThresholdBytes { get; set; } = 200_000;

    /// <summary>Gets or sets the maximum DataBus attachment size in bytes.</summary>
    public int DataBusMaximumAttachmentBytes { get; set; } = 50_000_000;

    /// <summary>Gets or sets the maximum scheduled-send delay.</summary>
    public TimeSpan MaximumSchedulingDelay
    {
        get
        {
            return _maximumSchedulingDelay;
        }
        set
        {
            _maximumSchedulingDelay = value;
        }
    }

    /// <summary>Gets or sets the maximum scheduled-send delay in seconds for generated options.</summary>
    public int MaximumSchedulingDelaySeconds
    {
        get
        {
            return checked((int)_maximumSchedulingDelay.TotalSeconds);
        }
        set
        {
            _maximumSchedulingDelay = TimeSpan.FromSeconds(value);
        }
    }

    /// <summary>Gets or sets the resource lifecycle policy.</summary>
    public MessagingResourceLifecycle ResourceLifecycle { get; set; } = MessagingResourceLifecycle.CreateIfMissing;

    /// <summary>Gets or sets the host configuration key for the transport connection.</summary>
    public string? ConnectionConfigurationKey { get; set; }

    /// <summary>Gets or sets the host configuration key for managed identity settings.</summary>
    public string? ManagedIdentityConfigurationKey { get; set; }
}

/// <summary>Controls ownership of resources declared by a messaging network.</summary>
public enum MessagingResourceLifecycle
{
    /// <summary>Create missing resources and retain them when the host stops.</summary>
    CreateIfMissing,

    /// <summary>Do not create or delete resources.</summary>
    External
}
