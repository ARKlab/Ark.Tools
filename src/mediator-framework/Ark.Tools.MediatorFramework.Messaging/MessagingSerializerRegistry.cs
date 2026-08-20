// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Frozen;
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using MessagePack;

using ProtoBuf;

namespace Ark.MediatorFramework.Messaging;

/// <summary>Serializer implementation selected by an envelope content type.</summary>
public interface IMessagingCodec
{
    /// <summary>Gets the protocol implemented by this codec.</summary>
    SerializationProtocol Protocol { get; }

    /// <summary>Gets the wire content type.</summary>
    string ContentType { get; }

    /// <summary>Serializes a statically known contract value.</summary>
    /// <typeparam name="T">The contract type.</typeparam>
    /// <param name="output">The destination buffer.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="jsonTypeInfo">Source-generated JSON metadata for the contract.</param>
    void Serialize<T>(
        IBufferWriter<byte> output,
        T value,
        JsonTypeInfo<T>? jsonTypeInfo)
        where T : notnull;

    /// <summary>Deserializes a statically known contract value.</summary>
    /// <typeparam name="T">The contract type.</typeparam>
    /// <param name="payload">The payload sequence.</param>
    /// <param name="jsonTypeInfo">Source-generated JSON metadata for the contract.</param>
    /// <returns>The deserialized value.</returns>
    T Deserialize<T>(
        in ReadOnlySequence<byte> payload,
        JsonTypeInfo<T>? jsonTypeInfo)
        where T : notnull;
}

/// <summary>UTF-8 JSON messaging codec.</summary>
public sealed class MessagingJsonCodec : IMessagingCodec
{
    /// <summary>Creates a JSON codec.</summary>
    public MessagingJsonCodec()
    {
    }

    /// <inheritdoc />
    public SerializationProtocol Protocol => SerializationProtocol.Json;

    /// <inheritdoc />
    public string ContentType => MessagingContentTypes.Json;

    /// <inheritdoc />
    public void Serialize<T>(
        IBufferWriter<byte> output,
        T value,
        JsonTypeInfo<T>? jsonTypeInfo)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        using var writer = new Utf8JsonWriter(output);
        JsonSerializer.Serialize(writer, value, jsonTypeInfo);
    }

    /// <inheritdoc />
    public T Deserialize<T>(
        in ReadOnlySequence<byte> payload,
        JsonTypeInfo<T>? jsonTypeInfo)
        where T : notnull
    {
        try
        {
            ArgumentNullException.ThrowIfNull(jsonTypeInfo);
            var reader = new Utf8JsonReader(payload);
            return JsonSerializer.Deserialize(ref reader, jsonTypeInfo)
                ?? throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The JSON payload deserialized to null.");
        }
        catch (MessagingEnvelopeException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The JSON payload is malformed.");
        }
        catch (NotSupportedException)
        {
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The JSON payload is malformed.");
        }
    }
}

/// <summary>MessagePack messaging codec.</summary>
public sealed class MessagingMessagePackCodec : IMessagingCodec
{
    private readonly MessagePackSerializerOptions _options;

    /// <summary>Creates a MessagePack codec with untrusted-data protections enabled.</summary>
    public MessagingMessagePackCodec(MessagePackSerializerOptions? options = null)
    {
        _options = (options ?? MessagePackSerializerOptions.Standard)
            .WithSecurity(MessagePackSecurity.UntrustedData);
    }

    /// <inheritdoc />
    public SerializationProtocol Protocol => SerializationProtocol.MessagePack;

    /// <inheritdoc />
    public string ContentType => MessagingContentTypes.MessagePack;

    /// <inheritdoc />
    public void Serialize<T>(
        IBufferWriter<byte> output,
        T value,
        JsonTypeInfo<T>? jsonTypeInfo)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(value);
        MessagePackSerializer.Serialize(output, value, _options);
    }

    /// <inheritdoc />
    public T Deserialize<T>(
        in ReadOnlySequence<byte> payload,
        JsonTypeInfo<T>? jsonTypeInfo)
        where T : notnull
    {
        try
        {
            return MessagePackSerializer.Deserialize<T>(payload, _options)
                ?? throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The MessagePack payload deserialized to null.");
        }
        catch (Exception exception) when (exception is MessagePackSerializationException
                                           or InvalidOperationException
                                           or OverflowException
                                           or FormatException
                                           or ArgumentException)
        {
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The MessagePack payload is malformed.");
        }
    }
}

/// <summary>protobuf-net messaging codec.</summary>
public sealed class MessagingProtobufCodec : IMessagingCodec
{
    /// <inheritdoc />
    public SerializationProtocol Protocol => SerializationProtocol.Protobuf;

    /// <inheritdoc />
    public string ContentType => MessagingContentTypes.Protobuf;

    /// <inheritdoc />
    [UnconditionalSuppressMessage("Trimming", "IL2091", Justification = "Contracts are closed generic types registered by generated metadata.")]
    public void Serialize<T>(
        IBufferWriter<byte> output,
        T value,
        JsonTypeInfo<T>? jsonTypeInfo)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(value);
        Serializer.Serialize(output, value);
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("Trimming", "IL2091", Justification = "Contracts are closed generic types registered by generated metadata.")]
    public T Deserialize<T>(
        in ReadOnlySequence<byte> payload,
        JsonTypeInfo<T>? jsonTypeInfo)
        where T : notnull
    {
        try
        {
            return Serializer.Deserialize<T>(payload)
                ?? throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The protobuf payload deserialized to null.");
        }
        catch (Exception exception) when (exception is ProtoException
                                           or InvalidOperationException
                                           or OverflowException
                                           or EndOfStreamException
                                           or ArgumentException)
        {
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The protobuf payload is malformed.");
        }
    }
}

/// <summary>Registry of installed messaging codecs.</summary>
public sealed class MessagingSerializerRegistry
{
    private readonly FrozenDictionary<string, IMessagingCodec> _byContentType;
    private readonly FrozenDictionary<SerializationProtocol, IMessagingCodec> _byProtocol;

    /// <summary>Creates a registry with the three built-in codecs installed.</summary>
    public MessagingSerializerRegistry(IEnumerable<IMessagingCodec>? codecs = null)
    {
        var byContentType = new Dictionary<string, IMessagingCodec>(StringComparer.OrdinalIgnoreCase);
        var byProtocol = new Dictionary<SerializationProtocol, IMessagingCodec>();
        foreach (var codec in codecs ?? _builtIns)
        {
            ArgumentNullException.ThrowIfNull(codec);
            if (!byProtocol.TryAdd(codec.Protocol, codec))
                throw new InvalidOperationException("A codec for this protocol or content type is already registered.");
            if (!byContentType.TryAdd(codec.ContentType, codec))
            {
                byProtocol.Remove(codec.Protocol);
                throw new InvalidOperationException("A codec for this protocol or content type is already registered.");
            }
        }

        _byContentType = byContentType.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _byProtocol = byProtocol.ToFrozenDictionary();
    }

    /// <summary>Resolves a codec by its protocol.</summary>
    public IMessagingCodec Resolve(SerializationProtocol protocol)
    {
        if (_byProtocol.TryGetValue(protocol, out var codec))
            return codec;
        throw new MessagingEnvelopeException(MessagingFailureKind.UnsupportedProtocol, "The requested serializer is not installed.");
    }

    /// <summary>Resolves a codec by its content type.</summary>
    public IMessagingCodec Resolve(string contentType)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentType);
        if (_byContentType.TryGetValue(contentType, out var codec))
            return codec;
        throw new MessagingEnvelopeException(MessagingFailureKind.UnsupportedProtocol, "The envelope content type is not supported.", MessagingHeaderNames.ContentType);
    }

    private static readonly IMessagingCodec[] _builtIns =
    [
        new MessagingJsonCodec(),
        new MessagingMessagePackCodec(),
        new MessagingProtobufCodec()
    ];
}
