// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Messaging;

/// <summary>Classifies fail-fast envelope and serialization failures.</summary>
public enum MessagingFailureKind
{
    /// <summary>The envelope metadata or payload is malformed.</summary>
    Malformed,

    /// <summary>The requested protocol is not installed or supported.</summary>
    UnsupportedProtocol,

    /// <summary>The contract is not present in the explicit contract registry.</summary>
    UnknownContract,

    /// <summary>The envelope was produced for a different messaging network.</summary>
    ForeignNetwork,

    /// <summary>An envelope or header limit was exceeded.</summary>
    SizeLimit
}

/// <summary>Exception raised when an envelope cannot be safely interpreted.</summary>
public sealed class MessagingEnvelopeException : InvalidOperationException
{
    /// <summary>Creates an envelope failure with the default malformed classification.</summary>
    public MessagingEnvelopeException()
        : this(MessagingFailureKind.Malformed, "The messaging envelope is invalid.")
    {
    }

    /// <summary>Creates an envelope failure with a message.</summary>
    public MessagingEnvelopeException(string message)
        : this(MessagingFailureKind.Malformed, message)
    {
    }

    /// <summary>Creates an envelope failure with a message and inner exception.</summary>
    public MessagingEnvelopeException(string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = MessagingFailureKind.Malformed;
    }

    /// <summary>Creates a bounded, serializable envelope failure.</summary>
    public MessagingEnvelopeException(MessagingFailureKind kind, string message, string? headerName = null)
        : base(message)
    {
        Kind = kind;
        HeaderName = headerName;
    }

    /// <summary>Gets the failure classification.</summary>
    public MessagingFailureKind Kind { get; }

    /// <summary>Gets the related header name, when applicable.</summary>
    public string? HeaderName { get; }
}

/// <summary>Bounds applied to envelope headers and inline payloads.</summary>
public sealed class MessagingEnvelopeLimits
{
    /// <summary>Default limits suitable for transport-neutral envelopes.</summary>
    public static MessagingEnvelopeLimits Default { get; } = new();

    /// <summary>Creates envelope limits.</summary>
    public MessagingEnvelopeLimits(
        int maximumHeaderCount = 64,
        int maximumHeaderNameLength = 128,
        int maximumHeaderValueLength = 4096,
        int maximumPayloadLength = 1_000_000)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumHeaderCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumHeaderNameLength, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumHeaderValueLength, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumPayloadLength);

        MaximumHeaderCount = maximumHeaderCount;
        MaximumHeaderNameLength = maximumHeaderNameLength;
        MaximumHeaderValueLength = maximumHeaderValueLength;
        MaximumPayloadLength = maximumPayloadLength;
    }

    /// <summary>Gets the maximum number of headers.</summary>
    public int MaximumHeaderCount { get; }

    /// <summary>Gets the maximum header name length.</summary>
    public int MaximumHeaderNameLength { get; }

    /// <summary>Gets the maximum header value length.</summary>
    public int MaximumHeaderValueLength { get; }

    /// <summary>Gets the maximum inline payload length.</summary>
    public int MaximumPayloadLength { get; }
}

/// <summary>Transport-neutral envelope headers and metadata.</summary>
public sealed class MessagingEnvelopeContext
{
    /// <summary>Creates an envelope context and copies its headers.</summary>
    public MessagingEnvelopeContext(
        IReadOnlyDictionary<string, string> headers,
        MessagingEnvelopeLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var effectiveLimits = limits ?? MessagingEnvelopeLimits.Default;
        if (headers.Count > effectiveLimits.MaximumHeaderCount)
            throw new MessagingEnvelopeException(MessagingFailureKind.SizeLimit, "The envelope header count exceeds its configured limit.");

        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var header in headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key) || header.Key.Length > effectiveLimits.MaximumHeaderNameLength)
                throw new MessagingEnvelopeException(MessagingFailureKind.SizeLimit, "An envelope header name is invalid or exceeds its configured limit.", header.Key);
            if (header.Value is null || header.Value.Length > effectiveLimits.MaximumHeaderValueLength)
                throw new MessagingEnvelopeException(MessagingFailureKind.SizeLimit, "An envelope header value exceeds its configured limit.", header.Key);
            if (!copy.TryAdd(header.Key, header.Value))
                throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "Envelope headers must have unique names.", header.Key);
        }

        Headers = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(copy);
    }

    /// <summary>Gets the immutable string metadata.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Gets a header value using ordinal comparison.</summary>
    public bool TryGetHeader(string name, out string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return Headers.TryGetValue(name, out value!);
    }

    /// <summary>Validates required native messaging headers.</summary>
    public void ValidateRequiredHeaders()
    {
        foreach (var name in _requiredHeaders)
        {
            if (!Headers.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
                throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "A required envelope header is missing.", name);
        }

        _ = MessagingEnvelope.ParseSentTime(Headers[MessagingHeaderNames.SentTime]);
        if (!Guid.TryParse(Headers[MessagingHeaderNames.MessageId], out _))
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The message identifier is not a valid GUID.", MessagingHeaderNames.MessageId);
        if (Headers.TryGetValue(MessagingHeaderNames.CorrelationId, out var correlationId)
            && !Guid.TryParse(correlationId, out _))
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The correlation identifier is not a valid GUID.", MessagingHeaderNames.CorrelationId);
    }

    internal void _validateLimits(MessagingEnvelopeLimits limits)
    {
        if (Headers.Count > limits.MaximumHeaderCount)
            throw new MessagingEnvelopeException(MessagingFailureKind.SizeLimit, "The envelope header count exceeds its configured limit.");
        foreach (var header in Headers)
        {
            if (header.Key.Length > limits.MaximumHeaderNameLength)
                throw new MessagingEnvelopeException(MessagingFailureKind.SizeLimit, "An envelope header name exceeds its configured limit.", header.Key);
            if (header.Value.Length > limits.MaximumHeaderValueLength)
                throw new MessagingEnvelopeException(MessagingFailureKind.SizeLimit, "An envelope header value exceeds its configured limit.", header.Key);
        }
    }

    private static readonly string[] _requiredHeaders =
    [
        MessagingHeaderNames.MessageType,
        MessagingHeaderNames.ContentType,
        MessagingHeaderNames.MessageId,
        MessagingHeaderNames.SentTime,
        MessagingHeaderNames.Network,
        MessagingHeaderNames.SenderIdentity
    ];
}

/// <summary>Transport-neutral binary payload paired with separate envelope metadata.</summary>
public sealed class MessagingEnvelope
{
    /// <summary>Creates an envelope and copies its payload.</summary>
    public MessagingEnvelope(
        MessagingEnvelopeContext context,
        ReadOnlyMemory<byte> payload,
        MessagingEnvelopeLimits? limits = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));

        var effectiveLimits = limits ?? MessagingEnvelopeLimits.Default;
        if (payload.Length > effectiveLimits.MaximumPayloadLength)
            throw new MessagingEnvelopeException(MessagingFailureKind.SizeLimit, "The envelope payload exceeds its configured limit.");

        Payload = payload.ToArray();
    }

    /// <summary>Gets the immutable envelope metadata.</summary>
    public MessagingEnvelopeContext Context { get; }

    /// <summary>Gets the copied binary payload.</summary>
    public byte[] Payload { get; }

    /// <summary>Formats a UTC timestamp for the sent-time header.</summary>
    public static string FormatSentTime(DateTimeOffset sentTime)
    {
        return sentTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    /// <summary>Parses the invariant UTC sent-time header.</summary>
    public static DateTimeOffset ParseSentTime(string value)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var result))
            throw new MessagingEnvelopeException(MessagingFailureKind.Malformed, "The sent-time header is not a valid invariant timestamp.", MessagingHeaderNames.SentTime);
        return result.ToUniversalTime();
    }
}
