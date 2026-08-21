// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

using MessagePack;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>MessagePack codec over a host-registered formatter resolver.</summary>
public sealed class MessagePackMessagingCodec : IMessagingCodec
{
    private readonly MessagePackSerializerOptions _serializeOptions;
    private readonly MessagePackSerializerOptions _deserializeOptions;

    /// <summary>Creates a codec from a host-registered formatter resolver.</summary>
    /// <param name="resolver">The formatter resolver.</param>
    public MessagePackMessagingCodec(IFormatterResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _serializeOptions = MessagePackSerializerOptions.Standard.WithResolver(resolver);
        _deserializeOptions = _serializeOptions.WithSecurity(MessagePackSecurity.UntrustedData);
    }

    /// <inheritdoc />
    public string ContentType => "application/x-msgpack";

    /// <inheritdoc />
    public SerializationProtocol Protocol => SerializationProtocol.MessagePack;

    /// <inheritdoc />
    public void Serialize<T>(T value, IBufferWriter<byte> writer) where T : class
    {
        ArgumentNullException.ThrowIfNull(writer);
        MessagePackSerializer.Serialize(writer, value, _serializeOptions);
    }

    /// <inheritdoc />
    public T Deserialize<T>(in ReadOnlySequence<byte> payload) where T : class
    {
        return MessagePackSerializer.Deserialize<T>(payload, _deserializeOptions)
            ?? throw new MessagePackSerializationException(
                $"Payload deserialized to null for contract '{typeof(T)}'.");
    }
}
