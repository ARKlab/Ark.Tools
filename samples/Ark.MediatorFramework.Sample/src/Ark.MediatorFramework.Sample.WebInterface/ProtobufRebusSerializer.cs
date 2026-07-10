// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Nodatime.Protobuf;

using ProtoBuf.Meta;

using Rebus.Messages;
using Rebus.Serialization;

namespace Ark.MediatorFramework.Sample.WebInterface;

internal sealed class ProtobufRebusSerializer : ISerializer
{
    private const string ContentType = "application/x-protobuf";
    private readonly RuntimeTypeModel _model;
    private readonly IReadOnlyDictionary<string, Type> _messageTypes;

    public ProtobufRebusSerializer(params Type[] messageTypes)
    {
        ArgumentNullException.ThrowIfNull(messageTypes);

        _model = RuntimeTypeModel.Create().AddNodaTimeSurrogates();
        _messageTypes = messageTypes.ToDictionary(
            type => type.FullName ?? throw new ArgumentException("Message types must have a full name.", nameof(messageTypes)),
            StringComparer.Ordinal);
    }

    public async Task<TransportMessage> Serialize(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageType = message.Body.GetType();
        var typeName = messageType.FullName
            ?? throw new InvalidOperationException("The Rebus message type must have a full name.");
        if (!_messageTypes.ContainsKey(typeName))
            throw new InvalidOperationException($"The Rebus message type '{typeName}' is not registered for Protobuf.");

        using var stream = new MemoryStream();
        _model.Serialize(stream, message.Body);

        var headers = new Dictionary<string, string>(message.Headers, StringComparer.Ordinal)
        {
            [Headers.Type] = typeName,
            [Headers.ContentType] = ContentType,
        };

        return await Task.FromResult(new TransportMessage(headers, stream.ToArray())).ConfigureAwait(false);
    }

    public async Task<Message> Deserialize(TransportMessage transportMessage)
    {
        ArgumentNullException.ThrowIfNull(transportMessage);

        if (!transportMessage.Headers.TryGetValue(Headers.ContentType, out var contentType)
            || !string.Equals(contentType, ContentType, StringComparison.Ordinal))
            throw new InvalidOperationException($"Expected Rebus content type '{ContentType}'.");

        if (!transportMessage.Headers.TryGetValue(Headers.Type, out var typeName)
            || !_messageTypes.TryGetValue(typeName, out var messageType))
            throw new InvalidOperationException("The Protobuf Rebus message type is missing or is not registered.");

        using var stream = new MemoryStream(transportMessage.Body, writable: false);
        var body = _model.Deserialize(stream, null, messageType)
            ?? throw new InvalidOperationException($"Could not deserialize Protobuf Rebus message '{typeName}'.");

        return await Task.FromResult(new Message(transportMessage.Headers, body)).ConfigureAwait(false);
    }
}
