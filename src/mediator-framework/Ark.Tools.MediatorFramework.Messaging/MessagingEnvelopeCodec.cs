// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

namespace Ark.MediatorFramework.Messaging;

/// <summary>Decoded contract value together with its resolved registry descriptor.</summary>
public sealed class MessagingDecodedMessage
{
    /// <summary>Creates a decoded message result.</summary>
    public MessagingDecodedMessage(MessagingContractDescriptor contract, object value)
    {
        Contract = contract ?? throw new ArgumentNullException(nameof(contract));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets the resolved contract descriptor.</summary>
    public MessagingContractDescriptor Contract { get; }

    /// <summary>Gets the deserialized contract value.</summary>
    public object Value { get; }
}

/// <summary>Builds and interprets transport-neutral messaging envelopes.</summary>
public sealed class MessagingEnvelopeCodec
{
    private static readonly HashSet<string> _reservedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        MessagingHeaderNames.MessageType,
        MessagingHeaderNames.ContentType,
        MessagingHeaderNames.ContentEncoding,
        MessagingHeaderNames.MessageId,
        MessagingHeaderNames.CorrelationId,
        MessagingHeaderNames.SentTime,
        MessagingHeaderNames.Network,
        MessagingHeaderNames.SenderIdentity,
        MessagingHeaderNames.PayloadAttachmentId,
        MessagingHeaderNames.PayloadAttachmentLength,
        MessagingHeaderNames.PayloadAttachmentSha256
    };

    private readonly MessagingContractRegistry _contracts;
    private readonly MessagingSerializerRegistry _serializers;
    private readonly MessagingEnvelopeLimits _limits;
    private readonly string? _networkIdentity;

    /// <summary>Creates an envelope codec.</summary>
    public MessagingEnvelopeCodec(
        MessagingContractRegistry contracts,
        MessagingSerializerRegistry? serializers = null,
        string? networkIdentity = null,
        MessagingEnvelopeLimits? limits = null)
    {
        _contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
        _serializers = serializers ?? new MessagingSerializerRegistry();
        _networkIdentity = networkIdentity;
        _limits = limits ?? MessagingEnvelopeLimits.Default;
    }

    /// <summary>Serializes a registered message or event using its owner-selected protocol.</summary>
    public MessagingEnvelope Create<T>(
        T value,
        string networkIdentity,
        string senderIdentity,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        Guid? messageId = null,
        Guid? correlationId = null,
        DateTimeOffset? sentTime = null)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrEmpty(networkIdentity);
        ArgumentException.ThrowIfNullOrEmpty(senderIdentity);

        var contract = _contracts.Resolve<T>();
        var serializer = _serializers.Resolve(contract.DefaultSerializer);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingHeaderNames.MessageType] = contract.Name,
            [MessagingHeaderNames.ContentType] = serializer.ContentType,
            [MessagingHeaderNames.MessageId] = (messageId ?? Guid.NewGuid()).ToString("D", CultureInfo.InvariantCulture),
            [MessagingHeaderNames.SentTime] = MessagingEnvelope.FormatSentTime(sentTime ?? DateTimeOffset.UtcNow),
            [MessagingHeaderNames.Network] = networkIdentity,
            [MessagingHeaderNames.SenderIdentity] = senderIdentity
        };
        if (correlationId is not null)
            headers[MessagingHeaderNames.CorrelationId] = correlationId.Value.ToString("D", CultureInfo.InvariantCulture);

        if (additionalHeaders is not null)
        {
            foreach (var header in additionalHeaders)
            {
                if (_reservedHeaders.Contains(header.Key))
                    throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "Reserved envelope headers cannot be overridden.", header.Key);
                if (!headers.TryAdd(header.Key, header.Value))
                    throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "Envelope headers must have unique names.", header.Key);
            }
        }

        var payload = new ArrayBufferWriter<byte>();
        contract._serialize(serializer, payload, value);
        return new MessagingEnvelope(new MessagingEnvelopeContext(headers, _limits), payload.WrittenMemory, _limits);
    }

    /// <summary>Deserializes an envelope using only its registered contract and content-type headers.</summary>
    public MessagingDecodedMessage Decode(MessagingEnvelope envelope)
    {
        var (contract, serializer) = _resolveIncoming(envelope);
        var payload = new ReadOnlySequence<byte>(envelope.Payload);
        return new MessagingDecodedMessage(contract, contract._deserialize(serializer, payload));
    }

    /// <summary>Deserializes an envelope and verifies the expected registered contract type.</summary>
    public T Decode<T>(MessagingEnvelope envelope)
        where T : notnull
    {
        var (actual, serializer) = _resolveIncoming(envelope);
        var expected = _contracts.Resolve<T>();
        if (!ReferenceEquals(actual, expected))
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The envelope contract type does not match the requested type.");

        var payload = new ReadOnlySequence<byte>(envelope.Payload);
        return expected._deserializeTyped(serializer, payload);
    }

    private (MessagingContractDescriptor Contract, IMessagingCodec Serializer) _resolveIncoming(MessagingEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Payload.Length > _limits.MaximumPayloadLength)
            throw new MessagingEnvelopeException(MessagingFailureKind.SizeLimit, "The envelope payload exceeds its configured limit.");
        envelope.Context._validateLimits(_limits);
        envelope.Context.ValidateRequiredHeaders();

        var network = envelope.Context.Headers[MessagingHeaderNames.Network];
        if (_networkIdentity is not null
            && !string.Equals(network, _networkIdentity, StringComparison.Ordinal))
            throw new MessagingEnvelopeException(MessagingFailureKind.ForeignNetwork, "The envelope belongs to a different messaging network.", MessagingHeaderNames.Network);

        var contract = _contracts.Resolve(envelope.Context.Headers[MessagingHeaderNames.MessageType]);
        var serializer = _serializers.Resolve(envelope.Context.Headers[MessagingHeaderNames.ContentType]);
        return (contract, serializer);
    }
}
