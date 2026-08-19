// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;

using MessagePack;

using ProtoBuf;

using Ark.MediatorFramework;

namespace Ark.MediatorFramework.Messaging;

/// <summary>Serializer implementation selected by an envelope content type.</summary>
public interface IMessagingCodec
{
    /// <summary>Gets the protocol implemented by this codec.</summary>
    SerializationProtocol Protocol { get; }

    /// <summary>Gets the wire content type.</summary>
    string ContentType { get; }

    /// <summary>Serializes a registered contract value.</summary>
    byte[] Serialize(Type contractType, object value);

    /// <summary>Deserializes bytes into a registered contract type.</summary>
    object Deserialize(Type contractType, ReadOnlyMemory<byte> payload);
}

/// <summary>UTF-8 JSON messaging codec.</summary>
public sealed class MessagingJsonCodec : IMessagingCodec
{
    private readonly JsonSerializerOptions _options;

    /// <summary>Creates a JSON codec with optional serializer options.</summary>
    public MessagingJsonCodec(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    /// <inheritdoc />
    public SerializationProtocol Protocol => SerializationProtocol.Json;

    /// <inheritdoc />
    public string ContentType => MessagingContentTypes.Json;

    /// <inheritdoc />
    public byte[] Serialize(Type contractType, object value)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.SerializeToUtf8Bytes(value, contractType, _options);
    }

    /// <inheritdoc />
    public object Deserialize(Type contractType, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        try
        {
            return JsonSerializer.Deserialize(payload.Span, contractType, _options)
                ?? throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The JSON payload deserialized to null.");
        }
        catch (MessagingEnvelopeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
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
    public byte[] Serialize(Type contractType, object value)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        ArgumentNullException.ThrowIfNull(value);
        return MessagePackSerializer.Serialize(contractType, value, _options);
    }

    /// <inheritdoc />
    public object Deserialize(Type contractType, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        try
        {
            return MessagePackSerializer.Deserialize(contractType, payload, _options)
                ?? throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The MessagePack payload deserialized to null.");
        }
        catch (MessagingEnvelopeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is MessagePackSerializationException or InvalidOperationException)
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
    public byte[] Serialize(Type contractType, object value)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        ArgumentNullException.ThrowIfNull(value);
        using var stream = new MemoryStream();
        Serializer.NonGeneric.Serialize(stream, value);
        return stream.ToArray();
    }

    /// <inheritdoc />
    public object Deserialize(Type contractType, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        try
        {
            using var stream = new MemoryStream(payload.ToArray(), writable: false);
            return Serializer.NonGeneric.Deserialize(contractType, stream)
                ?? throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The protobuf payload deserialized to null.");
        }
        catch (MessagingEnvelopeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ProtoException or EndOfStreamException)
        {
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The protobuf payload is malformed.");
        }
    }
}

/// <summary>Registry of installed messaging codecs.</summary>
public sealed class MessagingSerializerRegistry
{
    private readonly Dictionary<string, IMessagingCodec> _byContentType;
    private readonly Dictionary<SerializationProtocol, IMessagingCodec> _byProtocol;

    /// <summary>Creates a registry with the three built-in codecs installed.</summary>
    public MessagingSerializerRegistry(IEnumerable<IMessagingCodec>? codecs = null)
    {
        _byContentType = new Dictionary<string, IMessagingCodec>(StringComparer.OrdinalIgnoreCase);
        _byProtocol = new Dictionary<SerializationProtocol, IMessagingCodec>();
        foreach (var codec in codecs ?? new IMessagingCodec[]
        {
            new MessagingJsonCodec(),
            new MessagingMessagePackCodec(),
            new MessagingProtobufCodec()
        })
            Register(codec);
    }

    /// <summary>Registers an installed codec.</summary>
    public void Register(IMessagingCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        if (!_byProtocol.TryAdd(codec.Protocol, codec))
            throw new InvalidOperationException($"A codec for protocol '{codec.Protocol}' is already registered.");
        if (!_byContentType.TryAdd(codec.ContentType, codec))
        {
            _byProtocol.Remove(codec.Protocol);
            throw new InvalidOperationException($"A codec for content type '{codec.ContentType}' is already registered.");
        }
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
}
