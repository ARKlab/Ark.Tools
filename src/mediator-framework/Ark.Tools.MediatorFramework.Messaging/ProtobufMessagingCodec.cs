// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.IO.Pipelines;

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
    public async Task SerializeAsync<T>(
        T value,
        PipeWriter writer,
        CancellationToken ctk)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(writer);
        if (value is not IMessage message)
            throw new InvalidOperationException(
                $"Protobuf contract '{typeof(T)}' must implement Google.Protobuf.IMessage.");

        message.WriteTo(writer);
        await writer.FlushAsync(ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T> DeserializeAsync<T>(
        PipeReader reader,
        CancellationToken ctk)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(reader);
        ctk.ThrowIfCancellationRequested();
        var parse = ProtobufContractRegistry<T>.Parse
            ?? throw new InvalidOperationException(
                $"Protobuf contract '{typeof(T)}' has no registered MessageParser<T>.");
        var result = parse(reader.AsStream(leaveOpen: true));
        await Task.CompletedTask.ConfigureAwait(false);
        return result;
    }
}
