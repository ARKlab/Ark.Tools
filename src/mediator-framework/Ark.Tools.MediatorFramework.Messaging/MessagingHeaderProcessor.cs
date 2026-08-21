// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Classifies bounded message headers before generated typed dispatch.</summary>
public sealed class MessagingHeaderProcessor
{
    private readonly IMessagingCodecRegistry _codecs;
    private readonly string _networkIdentity;
    private readonly int _maximumHeaderCount;
    private readonly int _maximumHeaderKeyBytes;
    private readonly int _maximumHeaderValueBytes;

    /// <summary>Creates a header processor with the framework bounds.</summary>
    /// <param name="codecs">The installed codec registry.</param>
    /// <param name="networkIdentity">The local generated network identity.</param>
    public MessagingHeaderProcessor(
        IMessagingCodecRegistry codecs,
        string networkIdentity)
        : this(codecs, networkIdentity, 32, 128, 4096)
    {
    }

    /// <summary>Creates a header processor with explicit bounds.</summary>
    /// <param name="codecs">The installed codec registry.</param>
    /// <param name="networkIdentity">The local generated network identity.</param>
    /// <param name="maximumHeaderCount">The maximum number of headers.</param>
    /// <param name="maximumHeaderKeyBytes">The maximum UTF-8 key size.</param>
    /// <param name="maximumHeaderValueBytes">The maximum UTF-8 value size.</param>
    public MessagingHeaderProcessor(
        IMessagingCodecRegistry codecs,
        string networkIdentity,
        int maximumHeaderCount,
        int maximumHeaderKeyBytes,
        int maximumHeaderValueBytes)
    {
        ArgumentNullException.ThrowIfNull(codecs);
        ArgumentException.ThrowIfNullOrEmpty(networkIdentity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumHeaderCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumHeaderKeyBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumHeaderValueBytes);

        _codecs = codecs;
        _networkIdentity = networkIdentity;
        _maximumHeaderCount = maximumHeaderCount;
        _maximumHeaderKeyBytes = maximumHeaderKeyBytes;
        _maximumHeaderValueBytes = maximumHeaderValueBytes;
    }

    /// <summary>Validates headers and resolves the header-selected codec.</summary>
    /// <param name="headers">The transport-provided headers.</param>
    /// <returns>The selected codec and current logical contract name.</returns>
    public (IMessagingCodec Codec, string LogicalName) Classify(
        IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (headers.Count > _maximumHeaderCount
            || headers.Any(static header => header.Key is null || header.Value is null))
            throw new MessagingFailFastException(MessagingFailFastReason.OversizedHeaders);

        foreach (var header in headers)
        {
            if (Encoding.UTF8.GetByteCount(header.Key) > _maximumHeaderKeyBytes
                || Encoding.UTF8.GetByteCount(header.Value) > _maximumHeaderValueBytes)
                throw new MessagingFailFastException(MessagingFailFastReason.OversizedHeaders);
        }

        if (!headers.TryGetValue(MessagingHeaders.MessageType, out var logicalName)
            || string.IsNullOrEmpty(logicalName)
            || !headers.TryGetValue(MessagingHeaders.ContentType, out var contentType)
            || string.IsNullOrEmpty(contentType)
            || !headers.TryGetValue(MessagingHeaders.Network, out var network)
            || string.IsNullOrEmpty(network))
            throw new MessagingFailFastException(MessagingFailFastReason.MalformedHeaders);

        if (!string.Equals(network, _networkIdentity, StringComparison.Ordinal))
            throw new MessagingFailFastException(
                MessagingFailFastReason.ForeignNetwork,
                network);

        if (headers.TryGetValue(MessagingHeaders.RebusContentType, out var rebusContentType)
            && !string.Equals(contentType, rebusContentType, StringComparison.OrdinalIgnoreCase))
            throw new MessagingFailFastException(MessagingFailFastReason.MalformedHeaders);

        return (_codecs.GetByContentType(contentType), logicalName);
    }
}
