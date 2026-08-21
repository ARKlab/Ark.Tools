// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Counts bytes written and enforces a network payload limit.</summary>
public sealed class CountingBufferWriter : IBufferWriter<byte>
{
    private readonly IBufferWriter<byte> _inner;
    private readonly long _maximumPayloadBytes;
    private long _written;

    /// <summary>Creates a writer over a transport-owned writer.</summary>
    /// <param name="inner">The transport-owned writer.</param>
    /// <param name="maximumPayloadBytes">The maximum number of payload bytes.</param>
    public CountingBufferWriter(IBufferWriter<byte> inner, long maximumPayloadBytes)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPayloadBytes);

        _inner = inner;
        _maximumPayloadBytes = maximumPayloadBytes;
    }

    /// <summary>Gets the number of bytes advanced so far.</summary>
    public long BytesWritten => _written;

    /// <inheritdoc />
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > _maximumPayloadBytes - _written)
            throw new MessagingFailFastException(
                MessagingFailFastReason.OversizedPayload,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Payload exceeded the {0}-byte transport threshold.",
                    _maximumPayloadBytes));

        _inner.Advance(count);
        _written += count;
    }

    /// <inheritdoc />
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        return _inner.GetMemory(sizeHint);
    }

    /// <inheritdoc />
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        return _inner.GetSpan(sizeHint);
    }
}
