// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

using Google.Protobuf;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Protocol Buffers codec over generated Google.Protobuf contracts.</summary>
public sealed class ProtobufMessagingCodec : IMessagingCodec
{
    /// <inheritdoc />
    public string ContentType => "application/x-protobuf";

    /// <inheritdoc />
    public SerializationProtocol Protocol => SerializationProtocol.Protobuf;

    /// <inheritdoc />
    public void Serialize<T>(T value, IBufferWriter<byte> writer) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(writer);
        if (value is not IMessage message)
            throw new InvalidOperationException(
                $"Protobuf contract '{typeof(T)}' must implement Google.Protobuf.IMessage<T>.");

        message.WriteTo(writer);
    }

    /// <inheritdoc />
    public T Deserialize<T>(in ReadOnlySequence<byte> payload) where T : class
    {
        var parse = ProtobufContractRegistry<T>.Parse
            ?? throw new InvalidOperationException(
                $"Protobuf contract '{typeof(T)}' has no registered MessageParser<T>.");
        return parse(payload);
    }
}
