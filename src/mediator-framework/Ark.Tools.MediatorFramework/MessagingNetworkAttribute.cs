// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Declares a transport-neutral messaging network.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MessagingNetworkAttribute : Attribute
{
    /// <summary>The default maximum decompressed payload size in bytes.</summary>
    public const int DefaultMaximumDecompressedPayloadBytes = 1_000_000;

    /// <summary>The default maximum DataBus attachment size in bytes.</summary>
    public const int DefaultDataBusMaximumAttachmentBytes = 50_000_000;

    /// <summary>The default maximum scheduled-send delay in seconds.</summary>
    public const int DefaultMaximumSchedulingDelaySeconds = 604_800;

    private TimeSpan _maximumSchedulingDelay = TimeSpan.FromSeconds(DefaultMaximumSchedulingDelaySeconds);

    /// <summary>Gets or sets the participant types belonging to the network.</summary>
    public Type[] Members { get; set; } = Array.Empty<Type>();

    /// <summary>Gets or sets the optional capabilities required by the network.</summary>
    public MessagingCapabilities Requires { get; set; }

    /// <summary>Gets or sets the maximum decompressed payload size in bytes.</summary>
    public int MaximumDecompressedPayloadBytes { get; set; } = DefaultMaximumDecompressedPayloadBytes;

    /// <summary>Gets or sets the maximum DataBus attachment size in bytes.</summary>
    public int DataBusMaximumAttachmentBytes { get; set; } = DefaultDataBusMaximumAttachmentBytes;

    /// <summary>Gets or sets the maximum scheduled-send delay.</summary>
    public TimeSpan MaximumSchedulingDelay
    {
        get
        {
            return _maximumSchedulingDelay;
        }
        set
        {
            if (value < TimeSpan.Zero || value > TimeSpan.FromSeconds(int.MaxValue))
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "The maximum scheduling delay must fit in a non-negative integer number of seconds.");

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
            MaximumSchedulingDelay = TimeSpan.FromSeconds(value);
        }
    }

    /// <summary>Gets or sets the resource lifecycle policy.</summary>
    public MessagingResourceLifecycle ResourceLifecycle { get; set; } = MessagingResourceLifecycle.CreateIfMissing;

}

/// <summary>Controls ownership of resources declared by a messaging network.</summary>
public enum MessagingResourceLifecycle
{
    /// <summary>Create missing resources and retain them when the host stops.</summary>
    CreateIfMissing,

    /// <summary>Do not create or delete resources.</summary>
    External
}
