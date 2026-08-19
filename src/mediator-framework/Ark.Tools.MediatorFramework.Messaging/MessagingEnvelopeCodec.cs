// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

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
        MessagingHeaderNames.PayloadAttachmentSha256,
        MessagingHeaderNames.RebusDeliveryCount
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
        return Create(
            typeof(T),
            value,
            networkIdentity,
            senderIdentity,
            additionalHeaders,
            messageId,
            correlationId,
            sentTime);
    }

    /// <summary>Serializes a registered message or event using its owner-selected protocol.</summary>
    public MessagingEnvelope Create(
        Type contractType,
        object value,
        string networkIdentity,
        string senderIdentity,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        Guid? messageId = null,
        Guid? correlationId = null,
        DateTimeOffset? sentTime = null)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrEmpty(networkIdentity);
        ArgumentException.ThrowIfNullOrEmpty(senderIdentity);

        var contract = _contracts.Resolve(contractType);
        if (!contract.ContractType.IsInstanceOfType(value))
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The value does not match the registered contract type.");
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

        return new MessagingEnvelope(serializer.Serialize(contractType, value), headers, _limits);
    }

    /// <summary>Deserializes an envelope using only its registered contract and content-type headers.</summary>
    public MessagingDecodedMessage Decode(MessagingEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        _validateEnvelopeSize(envelope);
        _validateRequiredHeaders(envelope.Headers);

        var network = envelope.Headers[MessagingHeaderNames.Network];
        if (_networkIdentity is not null
            && !string.Equals(network, _networkIdentity, StringComparison.Ordinal))
            throw new MessagingEnvelopeException(MessagingFailureKind.ForeignNetwork, "The envelope belongs to a different messaging network.", MessagingHeaderNames.Network);

        var contract = _contracts.Resolve(envelope.Headers[MessagingHeaderNames.MessageType]);
        var contentType = envelope.Headers[MessagingHeaderNames.ContentType];
        if (envelope.Headers.TryGetValue(MessagingHeaderNames.RebusContentType, out var rebusContentType)
            && !string.Equals(contentType, rebusContentType, StringComparison.OrdinalIgnoreCase))
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "AMF and Rebus content-type headers conflict.", MessagingHeaderNames.ContentType);

        var serializer = _serializers.Resolve(contentType);
        try
        {
            return new MessagingDecodedMessage(contract, serializer.Deserialize(contract.ContractType, envelope.Payload));
        }
        catch (MessagingEnvelopeException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The envelope payload could not be deserialized.");
        }
    }

    /// <summary>Deserializes an envelope and verifies the expected registered contract type.</summary>
    public T Decode<T>(MessagingEnvelope envelope)
        where T : notnull
    {
        var result = Decode(envelope);
        if (result.Value is not T value)
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The envelope contract type does not match the requested type.");
        return value;
    }

    private static void _validateRequiredHeaders(IReadOnlyDictionary<string, string> headers)
    {
        foreach (var name in new[]
        {
            MessagingHeaderNames.MessageType,
            MessagingHeaderNames.ContentType,
            MessagingHeaderNames.MessageId,
            MessagingHeaderNames.SentTime,
            MessagingHeaderNames.Network,
            MessagingHeaderNames.SenderIdentity
        })
        {
            if (!headers.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
                throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "A required envelope header is missing.", name);
        }

        _ = MessagingEnvelope.ParseSentTime(headers[MessagingHeaderNames.SentTime]);
        if (!Guid.TryParse(headers[MessagingHeaderNames.MessageId], out _))
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The message identifier is not a valid GUID.", MessagingHeaderNames.MessageId);
        if (headers.TryGetValue(MessagingHeaderNames.CorrelationId, out var correlationId)
            && !Guid.TryParse(correlationId, out _))
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The correlation identifier is not a valid GUID.", MessagingHeaderNames.CorrelationId);
    }

    private void _validateEnvelopeSize(MessagingEnvelope envelope)
    {
        if (envelope.Payload.Length > _limits.MaximumPayloadLength)
            throw new MessagingEnvelopeException(MessagingFailureKind.SizeLimit, "The envelope payload exceeds its configured limit.");
        if (envelope.Headers.Count > _limits.MaximumHeaderCount)
            throw new MessagingEnvelopeException(MessagingFailureKind.SizeLimit, "The envelope header count exceeds its configured limit.");
        foreach (var header in envelope.Headers)
        {
            if (header.Key.Length > _limits.MaximumHeaderNameLength)
                throw new MessagingEnvelopeException(MessagingFailureKind.SizeLimit, "An envelope header name exceeds its configured limit.", header.Key);
            if (header.Value.Length > _limits.MaximumHeaderValueLength)
                throw new MessagingEnvelopeException(MessagingFailureKind.SizeLimit, "An envelope header value exceeds its configured limit.", header.Key);
        }
    }
}
