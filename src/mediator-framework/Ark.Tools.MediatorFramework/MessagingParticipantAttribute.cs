// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Declares how a participant joins a transport-neutral messaging network.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MessagingParticipantAttribute : Attribute
{
    /// <summary>Gets or sets the participant identity.</summary>
    public string? Identity { get; set; }

    /// <summary>Gets or sets messages processed by this participant.</summary>
    public Type[] Processes { get; set; } = Array.Empty<Type>();

    /// <summary>Gets or sets events published by this participant.</summary>
    public Type[] Publishes { get; set; } = Array.Empty<Type>();

    /// <summary>Gets or sets events subscribed to by this participant.</summary>
    public Type[] Subscribes { get; set; } = Array.Empty<Type>();

    /// <summary>Gets or sets serialization protocols supported by this participant.</summary>
    public SerializationProtocol[] Serializers { get; set; } = Array.Empty<SerializationProtocol>();

    /// <summary>Gets or sets the participant's write protocol.</summary>
    public SerializationProtocol DefaultSerializer { get; set; }

    /// <summary>Gets or sets the retry policy type.</summary>
    public Type? Retry { get; set; }

    /// <summary>Gets or sets the sender-side compression algorithm.</summary>
    public CompressionAlgorithm Compression { get; set; }

    /// <summary>Gets or sets the minimum payload size for compression.</summary>
    public int CompressionMinimumSizeBytes { get; set; }
}
