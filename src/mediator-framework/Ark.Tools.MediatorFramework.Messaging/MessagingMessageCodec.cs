// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

namespace Ark.MediatorFramework.Messaging;

/// <summary>Builds messaging headers and serializes transport-owned message bodies.</summary>
public sealed class MessagingMessageCodec
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
    private readonly MessagingMessageLimits _limits;
    private readonly string? _networkIdentity;

    /// <summary>Creates a message codec.</summary>
    public MessagingMessageCodec(
        MessagingContractRegistry contracts,
        MessagingSerializerRegistry serializers,
        string? networkIdentity = null,
        MessagingMessageLimits? limits = null)
    {
        _contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
        _serializers = serializers ?? throw new ArgumentNullException(nameof(serializers));
        _networkIdentity = networkIdentity;
        _limits = limits ?? MessagingMessageLimits.Default;
    }

    /// <summary>Creates transport-neutral headers for a registered message or event.</summary>
    public MessagingMessageContext CreateContext<T>(
        string networkIdentity,
        string senderIdentity,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        Guid? messageId = null,
        Guid? correlationId = null,
        DateTimeOffset? sentTime = null)
        where T : notnull
    {
        ArgumentException.ThrowIfNullOrEmpty(networkIdentity);
        ArgumentException.ThrowIfNullOrEmpty(senderIdentity);

        var contract = _contracts.Resolve<T>();
        var serializer = _serializers.Resolve(contract.DefaultSerializer);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingHeaderNames.MessageType] = contract.Name,
            [MessagingHeaderNames.ContentType] = serializer.ContentType,
            [MessagingHeaderNames.MessageId] = (messageId ?? Guid.NewGuid()).ToString("D", CultureInfo.InvariantCulture),
            [MessagingHeaderNames.SentTime] = MessagingMessageContext.FormatSentTime(sentTime ?? DateTimeOffset.UtcNow),
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
                    throw new MessagingProtocolException(MessagingFailureKind.Malformed, "Reserved message headers cannot be overridden.", header.Key);
                if (!headers.TryAdd(header.Key, header.Value))
                    throw new MessagingProtocolException(MessagingFailureKind.Malformed, "Message headers must have unique names.", header.Key);
            }
        }

        return new MessagingMessageContext(headers, _limits);
    }

    /// <summary>Serializes a registered value directly into a transport-owned body writer.</summary>
    public void Serialize<T>(IBufferWriter<byte> output, T value)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(value);
        var contract = _contracts.Resolve<T>();
        var serializer = _serializers.Resolve(contract.DefaultSerializer);
        serializer.Serialize(new LimitedBufferWriter(output, _limits.MaximumPayloadLength), value);
    }

    /// <summary>Deserializes a transport-owned body after validating its separate headers.</summary>
    public T Deserialize<T>(
        MessagingMessageContext context,
        in ReadOnlySequence<byte> payload)
        where T : notnull
    {
        var (actual, serializer) = _resolveIncoming(context, payload.Length);
        var expected = _contracts.Resolve<T>();
        if (!ReferenceEquals(actual, expected))
            throw new MessagingProtocolException(MessagingFailureKind.Malformed, "The message contract type does not match the requested type.");

        return serializer.Deserialize<T>(payload);
    }

    private (MessagingContractDescriptor Contract, IMessagingCodec Serializer) _resolveIncoming(
        MessagingMessageContext context,
        long payloadLength)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (payloadLength > _limits.MaximumPayloadLength)
            throw new MessagingProtocolException(MessagingFailureKind.SizeLimit, "The message body exceeds its configured limit.");
        context.ValidateLimits(_limits);
        context.ValidateRequiredHeaders();

        var network = context.Headers[MessagingHeaderNames.Network];
        if (_networkIdentity is not null
            && !string.Equals(network, _networkIdentity, StringComparison.Ordinal))
            throw new MessagingProtocolException(MessagingFailureKind.ForeignNetwork, "The message belongs to a different messaging network.", MessagingHeaderNames.Network);

        var contract = _contracts.Resolve(context.Headers[MessagingHeaderNames.MessageType]);
        var serializer = _serializers.Resolve(context.Headers[MessagingHeaderNames.ContentType]);
        return (contract, serializer);
    }

    private sealed class LimitedBufferWriter : IBufferWriter<byte>
    {
        private readonly IBufferWriter<byte> _inner;
        private readonly int _maximumLength;
        private int _written;

        public LimitedBufferWriter(IBufferWriter<byte> inner, int maximumLength)
        {
            _inner = inner;
            _maximumLength = maximumLength;
        }

        public void Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (count > _maximumLength - _written)
                throw new MessagingProtocolException(MessagingFailureKind.SizeLimit, "The message body exceeds its configured limit.");
            _inner.Advance(count);
            _written += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            return _inner.GetMemory(_validateSizeHint(sizeHint));
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            return _inner.GetSpan(_validateSizeHint(sizeHint));
        }

        private int _validateSizeHint(int sizeHint)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
            var remaining = _maximumLength - _written;
            if (remaining == 0 || sizeHint > remaining)
                throw new MessagingProtocolException(MessagingFailureKind.SizeLimit, "The message body exceeds its configured limit.");
            return sizeHint;
        }
    }
}
