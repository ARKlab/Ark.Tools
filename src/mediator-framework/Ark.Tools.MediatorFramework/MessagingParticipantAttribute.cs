// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Defines the generated static contract for a messaging participant declaration.</summary>
/// <remarks>
/// Generated participant declarations intentionally expose a static abstract identity member so the
/// generic declaration attributes can validate compile-time metadata without runtime reflection.
/// </remarks>
public interface IMessagingParticipantDeclaration
{
    /// <summary>Gets the generated participant identity.</summary>
    static abstract string Identity { get; }
}

/// <summary>Represents a participant declaration that resolves the generated static contract.</summary>
public interface IMessagingParticipant : IMessagingParticipantDeclaration
{
}

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

/// <summary>Generic declaration contract for a messaging participant.</summary>
/// <typeparam name="TDeclaration">The generated participant declaration.</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MessagingParticipantAttribute<TDeclaration> : Attribute
    where TDeclaration : class, IMessagingParticipantDeclaration
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
