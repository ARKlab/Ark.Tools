// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Messaging;

/// <summary>Classifies fail-fast message metadata and serialization failures.</summary>
public enum MessagingFailureKind
{
    /// <summary>The message metadata or body is malformed.</summary>
    Malformed,

    /// <summary>The requested protocol is not installed or supported.</summary>
    UnsupportedProtocol,

    /// <summary>The contract is not present in the explicit contract registry.</summary>
    UnknownContract,

    /// <summary>The message was produced for a different messaging network.</summary>
    ForeignNetwork,

    /// <summary>A message body or header limit was exceeded.</summary>
    SizeLimit
}

/// <summary>Exception raised when message metadata or a serialized body cannot be safely interpreted.</summary>
public sealed class MessagingProtocolException : InvalidOperationException
{
    /// <summary>Creates a protocol failure with the default malformed classification.</summary>
    public MessagingProtocolException()
        : this(MessagingFailureKind.Malformed, "The messaging metadata or body is invalid.")
    {
    }

    /// <summary>Creates a protocol failure with a message.</summary>
    public MessagingProtocolException(string message)
        : this(MessagingFailureKind.Malformed, message)
    {
    }

    /// <summary>Creates a protocol failure with a message and inner exception.</summary>
    public MessagingProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = MessagingFailureKind.Malformed;
    }

    /// <summary>Creates a bounded, serializable protocol failure.</summary>
    public MessagingProtocolException(MessagingFailureKind kind, string message, string? headerName = null)
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

/// <summary>Bounds applied to message headers and inline bodies.</summary>
public sealed class MessagingMessageLimits
{
    /// <summary>Default transport-neutral message limits.</summary>
    public static MessagingMessageLimits Default { get; } = new();

    /// <summary>Creates message limits.</summary>
    public MessagingMessageLimits(
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

/// <summary>Transport-neutral message headers and metadata.</summary>
public sealed class MessagingMessageContext
{
    /// <summary>Creates a message context and copies its headers.</summary>
    public MessagingMessageContext(
        IReadOnlyDictionary<string, string> headers,
        MessagingMessageLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var effectiveLimits = limits ?? MessagingMessageLimits.Default;
        if (headers.Count > effectiveLimits.MaximumHeaderCount)
            throw new MessagingProtocolException(MessagingFailureKind.SizeLimit, "The message header count exceeds its configured limit.");

        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var header in headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key) || header.Key.Length > effectiveLimits.MaximumHeaderNameLength)
                throw new MessagingProtocolException(MessagingFailureKind.SizeLimit, "A message header name is invalid or exceeds its configured limit.", header.Key);
            if (header.Value is null || header.Value.Length > effectiveLimits.MaximumHeaderValueLength)
                throw new MessagingProtocolException(MessagingFailureKind.SizeLimit, "A message header value exceeds its configured limit.", header.Key);
            if (!copy.TryAdd(header.Key, header.Value))
                throw new MessagingProtocolException(MessagingFailureKind.Malformed, "Message headers must have unique names.", header.Key);
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
            throw new MessagingProtocolException(MessagingFailureKind.Malformed, "The sent-time header is not a valid invariant timestamp.", MessagingHeaderNames.SentTime);
        return result.ToUniversalTime();
    }

    /// <summary>Validates required native messaging headers.</summary>
    public void ValidateRequiredHeaders()
    {
        foreach (var name in _requiredHeaders)
        {
            if (!Headers.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
                throw new MessagingProtocolException(MessagingFailureKind.Malformed, "A required message header is missing.", name);
        }

        _ = ParseSentTime(Headers[MessagingHeaderNames.SentTime]);
        if (!Guid.TryParse(Headers[MessagingHeaderNames.MessageId], out _))
            throw new MessagingProtocolException(MessagingFailureKind.Malformed, "The message identifier is not a valid GUID.", MessagingHeaderNames.MessageId);
        if (Headers.TryGetValue(MessagingHeaderNames.CorrelationId, out var correlationId)
            && !Guid.TryParse(correlationId, out _))
            throw new MessagingProtocolException(MessagingFailureKind.Malformed, "The correlation identifier is not a valid GUID.", MessagingHeaderNames.CorrelationId);
    }

    [SuppressMessage("Naming", "IDE1006", Justification = "Internal context validation follows public member naming.")]
    internal void ValidateLimits(MessagingMessageLimits limits)
    {
        if (Headers.Count > limits.MaximumHeaderCount)
            throw new MessagingProtocolException(MessagingFailureKind.SizeLimit, "The message header count exceeds its configured limit.");
        foreach (var header in Headers)
        {
            if (header.Key.Length > limits.MaximumHeaderNameLength)
                throw new MessagingProtocolException(MessagingFailureKind.SizeLimit, "A message header name exceeds its configured limit.", header.Key);
            if (header.Value.Length > limits.MaximumHeaderValueLength)
                throw new MessagingProtocolException(MessagingFailureKind.SizeLimit, "A message header value exceeds its configured limit.", header.Key);
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
