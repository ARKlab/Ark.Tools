// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.IO.Pipelines;

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
    public async Task SerializeAsync<T>(
        T value,
        PipeWriter writer,
        CancellationToken ctk)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(writer);
        await MessagePackSerializer
            .SerializeAsync(writer.AsStream(leaveOpen: true), value, _serializeOptions, ctk)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T> DeserializeAsync<T>(
        PipeReader reader,
        CancellationToken ctk)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(reader);
        return await MessagePackSerializer
            .DeserializeAsync<T>(reader.AsStream(leaveOpen: true), _deserializeOptions, ctk)
            .ConfigureAwait(false)
            ?? throw new MessagePackSerializationException(
                $"Payload deserialized to null for contract '{typeof(T)}'.");
    }
}
