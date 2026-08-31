// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Defines the generated static contract for a messaging network declaration.</summary>
/// <remarks>
/// Generated network declarations intentionally expose a static abstract identity member so the
/// generic declaration attributes can validate compile-time metadata without runtime reflection.
/// </remarks>
public interface IMessagingNetworkDeclaration
{
    /// <summary>Gets the resolved identity of the network.</summary>
    static abstract string NetworkIdentity { get; }
}

/// <summary>Represents a network declaration that resolves the generated static contract.</summary>
public interface IMessagingNetwork : IMessagingNetworkDeclaration
{
}

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

    /// <summary>Gets or sets the host configuration key for the transport connection.</summary>
    public string? ConnectionConfigurationKey { get; set; }

    /// <summary>Gets or sets the host configuration key for managed identity settings.</summary>
    public string? ManagedIdentityConfigurationKey { get; set; }
}

/// <summary>Generic declaration contract for a messaging network.</summary>
/// <typeparam name="TDeclaration">The generated network declaration.</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MessagingNetworkAttribute<TDeclaration> : Attribute
    where TDeclaration : class, IMessagingNetworkDeclaration
{
    /// <summary>Gets or sets the participant types belonging to the network.</summary>
    public Type[] Members { get; set; } = Array.Empty<Type>();

    /// <summary>Gets or sets the optional capabilities required by the network.</summary>
    public MessagingCapabilities Requires { get; set; }

    /// <summary>Gets or sets the maximum decompressed payload size in bytes.</summary>
    public int MaximumDecompressedPayloadBytes { get; set; } = MessagingNetworkAttribute.DefaultMaximumDecompressedPayloadBytes;

    /// <summary>Gets or sets the maximum DataBus attachment size in bytes.</summary>
    public int DataBusMaximumAttachmentBytes { get; set; } = MessagingNetworkAttribute.DefaultDataBusMaximumAttachmentBytes;

    /// <summary>Gets or sets the maximum scheduled-send delay.</summary>
    public TimeSpan MaximumSchedulingDelay { get; set; } = TimeSpan.FromSeconds(MessagingNetworkAttribute.DefaultMaximumSchedulingDelaySeconds);

    /// <summary>Gets or sets the maximum scheduled-send delay in seconds for generated options.</summary>
    public int MaximumSchedulingDelaySeconds { get; set; } = MessagingNetworkAttribute.DefaultMaximumSchedulingDelaySeconds;

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
