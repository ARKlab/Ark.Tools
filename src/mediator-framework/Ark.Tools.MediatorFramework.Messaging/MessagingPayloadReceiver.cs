// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Runtime.InteropServices;
using System.IO.Compression;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Fetches, validates, and decompresses an incoming messaging payload.</summary>
public sealed class MessagingPayloadReceiver
{
    private readonly IMessagingDataBus _dataBus;
    private readonly MessagingNetworkOptions _network;

    /// <summary>Creates a payload receiver.</summary>
    /// <param name="dataBus">The shared DataBus provider.</param>
    /// <param name="network">The network payload limits.</param>
    public MessagingPayloadReceiver(IMessagingDataBus dataBus, MessagingNetworkOptions network)
    {
        _dataBus = dataBus ?? throw new ArgumentNullException(nameof(dataBus));
        _network = network ?? throw new ArgumentNullException(nameof(network));
        if (_network.MaximumDecompressedPayloadBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(network), "The maximum decompressed size must be positive.");
    }

    /// <summary>Prepares a payload using header-selected attachment and encoding settings.</summary>
    /// <param name="headers">The received framework headers.</param>
    /// <param name="transportPayload">The inline transport payload.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The bounded, decompressed payload.</returns>
    public async Task<ReadOnlySequence<byte>> PreparePayloadAsync(
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> transportPayload,
        CancellationToken ctk)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ctk.ThrowIfCancellationRequested();
        var payload = transportPayload;

        if (headers.TryGetValue(MessagingHeaders.PayloadAttachmentId, out var attachmentId))
        {
            if (string.IsNullOrEmpty(attachmentId)
                || !headers.TryGetValue(MessagingHeaders.PayloadAttachmentLength, out var lengthText)
                || !long.TryParse(lengthText, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedLength)
                || expectedLength < 0
                || !headers.TryGetValue(MessagingHeaders.PayloadAttachmentSha256, out var expectedSha256)
                || string.IsNullOrEmpty(expectedSha256)
                || expectedLength > _network.DataBusMaximumAttachmentBytes)
            {
                throw new MessagingFailFastException(
                    MessagingFailFastReason.MalformedHeaders,
                    "The payload attachment headers are invalid.");
            }

            var stream = await _dataBus
                .OpenReadAsync(attachmentId, expectedLength, expectedSha256, ctk)
                .ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                payload = await _readBoundedAsync(stream, expectedLength, ctk).ConfigureAwait(false);
            }
        }

        if (headers.TryGetValue(MessagingHeaders.ContentEncoding, out var encoding))
        {
            payload = encoding switch
            {
                "gzip" => await _decompressBoundedAsync(payload, useBrotli: false, ctk).ConfigureAwait(false),
                "br" => await _decompressBoundedAsync(payload, useBrotli: true, ctk).ConfigureAwait(false),
                _ => throw new MessagingFailFastException(
                    MessagingFailFastReason.UnsupportedContentEncoding,
                    encoding)
            };
        }
        else if (payload.Length > _network.MaximumDecompressedPayloadBytes)
        {
            throw new MessagingFailFastException(MessagingFailFastReason.OversizedPayload);
        }

        return payload;
    }

    private async Task<ReadOnlySequence<byte>> _readBoundedAsync(
        Stream stream,
        long expectedLength,
        CancellationToken ctk)
    {
        if (expectedLength > _network.DataBusMaximumAttachmentBytes)
            throw new MessagingFailFastException(MessagingFailFastReason.OversizedPayload);

        var output = new ArrayBufferWriter<byte>(
            checked((int)Math.Min(expectedLength, 81_920)));
        var rented = ArrayPool<byte>.Shared.Rent(81_920);
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(rented.AsMemory(), ctk).ConfigureAwait(false);
                if (read == 0)
                    break;

                if (output.WrittenCount > expectedLength - read)
                    throw new MessagingFailFastException(
                        MessagingFailFastReason.AttachmentIntegrityFailure,
                        "The payload attachment exceeded its envelope length.");
                rented.AsSpan(0, read).CopyTo(output.GetSpan(read));
                output.Advance(read);
            }

            if (output.WrittenCount != expectedLength)
                throw new MessagingFailFastException(
                    MessagingFailFastReason.AttachmentIntegrityFailure,
                    "The payload attachment ended before its envelope length.");
            return new ReadOnlySequence<byte>(output.WrittenMemory);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async Task<ReadOnlySequence<byte>> _decompressBoundedAsync(
        ReadOnlySequence<byte> compressed,
        bool useBrotli,
        CancellationToken ctk)
    {
        using var source = new SequenceReadStream(compressed);
        Stream decompressor = useBrotli
            ? new BrotliStream(source, CompressionMode.Decompress, leaveOpen: false)
            : new GZipStream(source, CompressionMode.Decompress, leaveOpen: false);
        await using (decompressor.ConfigureAwait(false))
        {
            return await _decompressAsync(decompressor, ctk).ConfigureAwait(false);
        }
    }

    private async Task<ReadOnlySequence<byte>> _decompressAsync(
        Stream decompressor,
        CancellationToken ctk)
    {

        var output = new ArrayBufferWriter<byte>();
        var rented = ArrayPool<byte>.Shared.Rent(
            Math.Min(81_920, _network.MaximumDecompressedPayloadBytes + 1));
        try
        {
            while (true)
            {
                var read = await decompressor.ReadAsync(rented.AsMemory(), ctk).ConfigureAwait(false);
                if (read == 0)
                    break;
                if (output.WrittenCount > _network.MaximumDecompressedPayloadBytes - read)
                    throw new MessagingFailFastException(MessagingFailFastReason.OversizedPayload);
                rented.AsSpan(0, read).CopyTo(output.GetSpan(read));
                output.Advance(read);
            }

            return new ReadOnlySequence<byte>(output.WrittenMemory);
        }
        catch (MessagingFailFastException)
        {
            throw;
        }
        catch (InvalidDataException exception)
        {
            throw new MessagingFailFastException(
                MessagingFailFastReason.InvalidCompressedPayload,
                exception.Message);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private sealed class SequenceReadStream : MemoryStream
    {
        internal SequenceReadStream(ReadOnlySequence<byte> sequence)
            : this(_getSegment(sequence))
        {
        }

        private SequenceReadStream(ArraySegment<byte> segment)
            : base(segment.Array ?? Array.Empty<byte>(), segment.Offset, segment.Count, writable: false)
        {
        }

        private static ArraySegment<byte> _getSegment(ReadOnlySequence<byte> sequence)
        {
            if (sequence.IsSingleSegment
                && MemoryMarshal.TryGetArray(sequence.First, out var segment))
            {
                return segment;
            }

            return new ArraySegment<byte>(sequence.ToArray());
        }
    }
}
