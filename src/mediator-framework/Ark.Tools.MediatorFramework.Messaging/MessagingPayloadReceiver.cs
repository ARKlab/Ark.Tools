// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
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

    /// <summary>Opens a payload using header-selected attachment and encoding settings.</summary>
    /// <param name="headers">The received framework headers.</param>
    /// <param name="transportPayload">The inline transport payload.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A bounded payload stream owned by the caller.</returns>
    public async Task<Stream> PreparePayloadAsync(
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> transportPayload,
        CancellationToken ctk)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ctk.ThrowIfCancellationRequested();
        var contentEncoding = headers.TryGetValue(
            MessagingHeaders.ContentEncoding,
            out var encoding)
            ? encoding
            : null;
        if (contentEncoding is not null
            && contentEncoding is not "gzip"
            && contentEncoding is not "br")
        {
            throw new MessagingFailFastException(
                MessagingFailFastReason.UnsupportedContentEncoding,
                contentEncoding);
        }

#pragma warning disable CA2000 // Ownership is transferred to the returned stream or released in the finally block.
        Stream? payload = new SequenceReadStream(transportPayload);
#pragma warning restore CA2000
        try
        {
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

                if (contentEncoding is null
                    && expectedLength > _network.MaximumDecompressedPayloadBytes)
                {
                    throw new MessagingFailFastException(MessagingFailFastReason.OversizedPayload);
                }

                await payload.DisposeAsync().ConfigureAwait(false);
                payload = null;
                payload = await _dataBus
                    .OpenReadAsync(attachmentId, expectedLength, expectedSha256, ctk)
                    .ConfigureAwait(false);
            }

            var result = new BoundedPayloadReadStream(
                payload,
                contentEncoding,
                _network.MaximumDecompressedPayloadBytes);
            payload = null;
            return result;
        }
        finally
        {
            if (payload is not null)
                await payload.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class BoundedPayloadReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _maximumBytes;
        private long _read;
        private bool _disposed;

        internal BoundedPayloadReadStream(
            Stream payload,
            string? contentEncoding,
            long maximumBytes)
        {
            _inner = contentEncoding switch
            {
                "gzip" => new GZipStream(payload, CompressionMode.Decompress, leaveOpen: false),
                "br" => new BrotliStream(payload, CompressionMode.Decompress, leaveOpen: false),
                _ => payload
            };
            _maximumBytes = maximumBytes;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _read;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty)
                return 0;

            try
            {
                var read = _inner.Read(buffer);
                _validate(read);
                return read;
            }
            catch (InvalidDataException exception)
            {
                throw _invalidCompressedPayload(exception);
            }
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty)
                return 0;

            try
            {
                var read = await _inner
                    .ReadAsync(buffer, cancellationToken)
                    .ConfigureAwait(false);
                _validate(read);
                return read;
            }
            catch (InvalidDataException exception)
            {
                throw _invalidCompressedPayload(exception);
            }
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            return await ReadAsync(
                buffer.AsMemory(offset, count),
                cancellationToken).ConfigureAwait(false);
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            try
            {
                if (!_disposed)
                {
                    _disposed = true;
                    await _inner.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                await base.DisposeAsync().ConfigureAwait(false);
            }
        }

        private void _validate(int read)
        {
            if (read == 0)
                return;

            if (_read + read > _maximumBytes)
                throw new MessagingFailFastException(MessagingFailFastReason.OversizedPayload);

            _read += read;
        }

        private static MessagingFailFastException _invalidCompressedPayload(InvalidDataException exception)
        {
            return new MessagingFailFastException(
                MessagingFailFastReason.InvalidCompressedPayload,
                exception.Message);
        }
    }

    private sealed class SequenceReadStream : Stream
    {
        private readonly ReadOnlySequence<byte> _sequence;
        private SequencePosition _position;
        private ReadOnlyMemory<byte> _current;
        private int _offset;
        private bool _started;
        private long _read;

        internal SequenceReadStream(ReadOnlySequence<byte> sequence)
        {
            _sequence = sequence;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _sequence.Length;
        public override long Position
        {
            get => _read;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty)
                return 0;

            var written = 0;
            while (written < buffer.Length && _moveNext())
            {
                var available = _current.Length - _offset;
                var copy = Math.Min(available, buffer.Length - written);
                _current.Span.Slice(_offset, copy).CopyTo(buffer.Slice(written, copy));
                _offset += copy;
                written += copy;
            }

            _read += written;
            return written;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        private bool _moveNext()
        {
            if (_offset < _current.Length)
                return true;

            if (!_started)
            {
                _position = _sequence.Start;
                _started = true;
            }

            if (!_sequence.TryGet(ref _position, out _current, advance: true))
                return false;

            _offset = 0;
            return true;
        }
    }
}
