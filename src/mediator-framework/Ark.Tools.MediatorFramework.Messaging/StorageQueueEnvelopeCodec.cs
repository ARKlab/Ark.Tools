// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Buffers.Text;
using System.Collections.ObjectModel;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Encodes complete Azure Storage Queue envelopes with one Base64 operation.</summary>
public static class StorageQueueEnvelopeCodec
{
    private const int _maximumHeaderCount = 1_024;

    /// <summary>Encodes headers and an opaque binary payload as Base64 text.</summary>
    /// <param name="headers">The complete envelope headers.</param>
    /// <param name="payload">The opaque serialized payload.</param>
    /// <returns>The single-Base64 encoded envelope text.</returns>
    public static string Encode(
        IReadOnlyDictionary<string, string> headers,
        in ReadOnlySequence<byte> payload)
    {
        return _encode(headers, payload, StorageQueueLimits.MaximumNormalCanonicalBytes);
    }

    /// <summary>Decodes raw Base64 queue-message data into headers and an opaque payload.</summary>
    /// <param name="rawBody">The raw body supplied by a none-encoded QueueTrigger.</param>
    /// <returns>The decoded headers and payload.</returns>
    public static StorageQueueEnvelope Decode(BinaryData rawBody)
    {
        ArgumentNullException.ThrowIfNull(rawBody);

        var text = rawBody.ToMemory();
        var canonical = new byte[Base64.GetMaxDecodedFromUtf8Length(text.Length)];
        if (Base64.DecodeFromUtf8(text.Span, canonical, out var consumed, out var written)
                != OperationStatus.Done
            || consumed != text.Length)
        {
            throw new MessagingFailFastException(
                MessagingFailFastReason.MalformedHeaders,
                "The Storage Queue envelope is not valid Base64 text.");
        }

        return _decodeCanonical(canonical.AsMemory(0, written));
    }

    internal static string _encodePoison(
        BinaryData rawBody,
        string originalMessageId,
        string reason,
        string description)
    {
        ArgumentNullException.ThrowIfNull(rawBody);
        ArgumentException.ThrowIfNullOrEmpty(originalMessageId);
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentNullException.ThrowIfNull(description);

        StorageQueueEnvelope envelope;
        try
        {
            envelope = Decode(rawBody);
        }
        catch (MessagingFailFastException)
        {
            envelope = new StorageQueueEnvelope(
                new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(StringComparer.Ordinal)),
                new ReadOnlySequence<byte>(rawBody.ToMemory()));
        }

        var headers = envelope.Headers.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal);
        headers[StorageQueuePoisonHeaders.OriginalMessageId] = _boundUtf8(originalMessageId, 512);
        headers[StorageQueuePoisonHeaders.Reason] = _boundUtf8(reason, 256);
        headers[StorageQueuePoisonHeaders.Description] = _boundUtf8(description, 1_024);
        return _encode(headers, envelope.Payload, StorageQueueLimits.MaximumPoisonCanonicalBytes);
    }

    internal static int _measureCanonical(
        IReadOnlyDictionary<string, string> headers,
        in ReadOnlySequence<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (headers.Count > _maximumHeaderCount)
            throw new ArgumentOutOfRangeException(nameof(headers), "The envelope has too many headers.");

        var size = _varIntLength(headers.Count);
        checked
        {
            foreach (var pair in headers)
            {
                var keyLength = Encoding.UTF8.GetByteCount(pair.Key);
                var valueLength = Encoding.UTF8.GetByteCount(pair.Value);
                size += _varIntLength(keyLength) + keyLength;
                size += _varIntLength(valueLength) + valueLength;
            }

            size += checked((int)payload.Length);
        }

        return size;
    }

    private static string _encode(
        IReadOnlyDictionary<string, string> headers,
        in ReadOnlySequence<byte> payload,
        int maximumCanonicalBytes)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var canonicalSize = _measureCanonical(headers, payload);
        if (canonicalSize > maximumCanonicalBytes)
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                "The completed Storage Queue envelope exceeds its canonical size limit.");

        var canonical = new ArrayBufferWriter<byte>(Math.Max(canonicalSize, 1));
        _writeVarInt(canonical, headers.Count);
        foreach (var pair in headers.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            _writeUtf8(canonical, pair.Key);
            _writeUtf8(canonical, pair.Value);
        }
        foreach (var segment in payload)
            canonical.Write(segment.Span);

        var encoded = new byte[Base64.GetMaxEncodedToUtf8Length(canonical.WrittenCount)];
        var status = Base64.EncodeToUtf8(
            canonical.WrittenSpan,
            encoded,
            out var consumed,
            out var written);
        if (status != OperationStatus.Done || consumed != canonical.WrittenCount)
            throw new InvalidOperationException("The Storage Queue envelope could not be Base64 encoded.");
        if (written > StorageQueueLimits.MaximumEncodedTextBytes)
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                "The completed Storage Queue message exceeds 64 KiB.");

        return Encoding.UTF8.GetString(encoded.AsSpan(0, written));
    }

    private static StorageQueueEnvelope _decodeCanonical(ReadOnlyMemory<byte> canonical)
    {
        var offset = 0;
        var headerCount = _readVarInt(canonical.Span, ref offset);
        if (headerCount > _maximumHeaderCount)
            throw _malformed("The Storage Queue envelope has too many headers.");

        var headers = new Dictionary<string, string>(headerCount, StringComparer.Ordinal);
        for (var index = 0; index < headerCount; index++)
        {
            var key = _readUtf8(canonical.Span, ref offset);
            var value = _readUtf8(canonical.Span, ref offset);
            if (!headers.TryAdd(key, value))
                throw _malformed("The Storage Queue envelope contains a duplicate header.");
        }

        return new StorageQueueEnvelope(
            new ReadOnlyDictionary<string, string>(headers),
            new ReadOnlySequence<byte>(canonical[offset..]));
    }

    private static string _readUtf8(ReadOnlySpan<byte> source, ref int offset)
    {
        var length = _readVarInt(source, ref offset);
        if (length > source.Length - offset)
            throw _malformed("The Storage Queue envelope contains a truncated header.");

        try
        {
            var value = new UTF8Encoding(false, true).GetString(source.Slice(offset, length));
            offset += length;
            return value;
        }
        catch (DecoderFallbackException exception)
        {
            throw new MessagingFailFastException(
                MessagingFailFastReason.MalformedHeaders,
                "The Storage Queue envelope contains invalid UTF-8.",
                exception);
        }
    }

    private static int _readVarInt(ReadOnlySpan<byte> source, ref int offset)
    {
        uint value = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            if (offset >= source.Length)
                throw _malformed("The Storage Queue envelope contains a truncated length.");

            var current = source[offset++];
            value |= (uint)(current & 0x7f) << shift;
            if ((current & 0x80) == 0)
            {
                if (value > int.MaxValue)
                    throw _malformed("The Storage Queue envelope contains an invalid length.");
                return (int)value;
            }
        }

        throw _malformed("The Storage Queue envelope contains an invalid length.");
    }

    private static void _writeUtf8(IBufferWriter<byte> writer, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var length = Encoding.UTF8.GetByteCount(value);
        _writeVarInt(writer, length);
        var target = writer.GetSpan(length);
        var written = Encoding.UTF8.GetBytes(value, target);
        writer.Advance(written);
    }

    private static void _writeVarInt(IBufferWriter<byte> writer, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        var remaining = (uint)value;
        do
        {
            var current = (byte)(remaining & 0x7f);
            remaining >>= 7;
            if (remaining != 0)
                current |= 0x80;
            writer.GetSpan(1)[0] = current;
            writer.Advance(1);
        }
        while (remaining != 0);
    }

    private static int _varIntLength(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        var length = 1;
        var remaining = (uint)value;
        while ((remaining >>= 7) != 0)
            length++;
        return length;
    }

    private static string _boundUtf8(string value, int maximumBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maximumBytes)
            return value;

        var length = Math.Min(value.Length, maximumBytes);
        while (length > 0 && Encoding.UTF8.GetByteCount(value.AsSpan(0, length)) > maximumBytes)
            length--;
        return value[..length];
    }

    private static MessagingFailFastException _malformed(string message)
    {
        return new MessagingFailFastException(MessagingFailFastReason.MalformedHeaders, message);
    }
}

/// <summary>Contains a decoded Storage Queue envelope.</summary>
public sealed class StorageQueueEnvelope
{
    internal StorageQueueEnvelope(
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload)
    {
        Headers = headers;
        Payload = payload;
    }

    /// <summary>Gets the decoded envelope headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Gets the opaque decoded payload.</summary>
    public ReadOnlySequence<byte> Payload { get; }
}

/// <summary>Defines metadata headers added to SDK-moved poison messages.</summary>
public static class StorageQueuePoisonHeaders
{
    /// <summary>Gets the original Azure Storage Queue message identifier header.</summary>
    public const string OriginalMessageId = "amf1-poison-original-message-id";

    /// <summary>Gets the poison reason header.</summary>
    public const string Reason = "amf1-poison-reason";

    /// <summary>Gets the bounded poison description header.</summary>
    public const string Description = "amf1-poison-description";
}
