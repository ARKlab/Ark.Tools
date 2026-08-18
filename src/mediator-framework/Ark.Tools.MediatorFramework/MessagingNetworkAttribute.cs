// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Declares the shared, transport-neutral settings for a messaging network profile.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MessagingNetworkAttribute : Attribute
{
    /// <summary>Initializes a network declaration.</summary>
    /// <param name="requires">Capabilities required by the network.</param>
    public MessagingNetworkAttribute(MessagingCapabilities requires = MessagingCapabilities.None)
    {
        Requires = requires;
    }

    /// <summary>Capabilities required by the network.</summary>
    public MessagingCapabilities Requires { get; }

    /// <summary>Protocols accepted when receiving payloads.</summary>
    public SerializationProtocol[] Serializers { get; set; } = new[] { SerializationProtocol.Json };

    /// <summary>Protocol used for newly sent payloads.</summary>
    public SerializationProtocol DefaultSerializer { get; set; } = SerializationProtocol.Json;

    /// <summary>Compression algorithm applied to payloads.</summary>
    public CompressionAlgorithm Compression { get; set; } = CompressionAlgorithm.None;

    /// <summary>Minimum payload size in bytes before compression is applied.</summary>
    public int CompressionMinimumSizeBytes { get; set; }

    /// <summary>Maximum transport payload size before DataBus offload.</summary>
    public int MaximumTransportPayloadBytes { get; set; } = 240_000;

    /// <summary>Maximum decompressed payload size accepted by a receiver.</summary>
    public int MaximumDecompressedPayloadBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>Payload size at which DataBus claim-check is used.</summary>
    public int DataBusOffloadThresholdBytes { get; set; } = 240_000;

    /// <summary>Maximum DataBus attachment size.</summary>
    public int MaximumDataBusAttachmentBytes { get; set; } = 64 * 1024 * 1024;

    /// <summary>Retry policy type used by the network.</summary>
    public Type? RetryPolicy { get; set; }

    /// <summary>Additional lock-renewal seconds beyond the handler duration.</summary>
    public int LockRenewalBufferSeconds { get; set; } = 60;

    /// <summary>Maximum scheduling delay in seconds accepted by the network.</summary>
    public int MaximumSchedulingDelaySeconds { get; set; } = 7 * 24 * 60 * 60;

    /// <summary>Broker resource ownership policy.</summary>
    public MessagingResourceLifecycle ResourceLifecycle { get; set; } = MessagingResourceLifecycle.External;

    /// <summary>Configuration key containing the transport connection.</summary>
    public string ConnectionConfigurationKey { get; set; } = "Messaging:Connection";

    /// <summary>Configuration key containing the managed-identity resource.</summary>
    public string ManagedIdentityConfigurationKey { get; set; } = "Messaging:ManagedIdentity";
}
