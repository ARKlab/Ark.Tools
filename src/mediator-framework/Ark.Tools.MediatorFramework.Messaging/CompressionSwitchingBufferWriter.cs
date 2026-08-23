// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.IO.Compression;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Buffers a small prefix and switches to compression when its threshold is reached.</summary>
public sealed class CompressionSwitchingBufferWriter : IBufferWriter<byte>
{
    private readonly IBufferWriter<byte> _output;
    private readonly CompressionAlgorithm _algorithm;
    private readonly int _minimumSizeBytes;
    private readonly long _maximumPayloadBytes;
    private ArrayBufferWriter<byte>? _pending = new();
    private ArrayBufferWriter<byte>? _staging;
    private Stream? _compressionStream;
    private bool _completed;
    private bool _compressed;
    private long _bytesWritten;

    /// <summary>Creates a compression-switching writer.</summary>
    /// <param name="output">The final transport-owned output writer.</param>
    /// <param name="algorithm">The sender-side compression algorithm.</param>
    /// <param name="minimumSizeBytes">The size at which compression starts.</param>
    /// <param name="maximumPayloadBytes">The maximum final payload size.</param>
    public CompressionSwitchingBufferWriter(
        IBufferWriter<byte> output,
        CompressionAlgorithm algorithm,
        int minimumSizeBytes,
        long maximumPayloadBytes)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumSizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPayloadBytes);

        _output = output;
        _algorithm = algorithm;
        _minimumSizeBytes = minimumSizeBytes;
        _maximumPayloadBytes = maximumPayloadBytes;
    }

    /// <summary>Gets whether compression was selected.</summary>
    public bool Compressed => _compressed;

    /// <summary>Gets the final number of bytes written.</summary>
    public long BytesWritten => _bytesWritten;

    /// <inheritdoc />
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (_completed)
            throw new InvalidOperationException("The compression writer has already completed.");

        if (_compressed)
        {
            var staging = _staging!;
            staging.Advance(count);
            _compressionStream!.Write(staging.WrittenSpan);
            _staging = new ArrayBufferWriter<byte>();
            return;
        }

        var pending = _pending!;
        pending.Advance(count);
        if (_algorithm != CompressionAlgorithm.None
            && pending.WrittenCount >= _minimumSizeBytes)
        {
            _startCompression();
        }
    }

    /// <inheritdoc />
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        _ensureWritable();
        return _compressed
            ? _staging!.GetMemory(sizeHint)
            : _pending!.GetMemory(sizeHint);
    }

    /// <inheritdoc />
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        _ensureWritable();
        return _compressed
            ? _staging!.GetSpan(sizeHint)
            : _pending!.GetSpan(sizeHint);
    }

    /// <summary>Flushes the final compression frame and completes the writer.</summary>
    public void Complete()
    {
        if (_completed)
            return;

        if (!_compressed
            && _algorithm != CompressionAlgorithm.None
            && _pending!.WrittenCount >= _minimumSizeBytes)
        {
            _startCompression();
        }

        if (_compressed)
        {
            if (_staging!.WrittenCount > 0)
            {
                _compressionStream!.Write(_staging.WrittenSpan);
                _staging = new ArrayBufferWriter<byte>();
            }

            _compressionStream!.Dispose();
            _compressionStream = null;
        }
        else
        {
            _writeOutput(_pending!.WrittenSpan);
        }

        _completed = true;
    }

    private void _startCompression()
    {
        _compressed = true;
        _staging = new ArrayBufferWriter<byte>();
        _compressionStream = _algorithm switch
        {
            CompressionAlgorithm.Gzip => new GZipStream(
                new BufferWriterStream(_output, _maximumPayloadBytes, value => _bytesWritten = value),
                CompressionLevel.Fastest),
            CompressionAlgorithm.Brotli => new BrotliStream(
                new BufferWriterStream(_output, _maximumPayloadBytes, value => _bytesWritten = value),
                CompressionLevel.Fastest),
            _ => throw new InvalidOperationException("Compression is not configured.")
        };

        _compressionStream.Write(_pending!.WrittenSpan);
        _pending = null;
    }

    private void _writeOutput(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > _maximumPayloadBytes - _bytesWritten)
            throw _oversized();

        bytes.CopyTo(_output.GetSpan(bytes.Length));
        _output.Advance(bytes.Length);
        _bytesWritten += bytes.Length;
    }

    private void _ensureWritable()
    {
        if (_completed)
            throw new InvalidOperationException("The compression writer has already completed.");
    }

    private MessagingFailFastException _oversized()
    {
        return new MessagingFailFastException(
            MessagingFailFastReason.OversizedPayload,
            string.Format(
                CultureInfo.InvariantCulture,
                "Payload exceeded the {0}-byte attachment threshold.",
                _maximumPayloadBytes));
    }

    private sealed class BufferWriterStream : Stream
    {
        private readonly IBufferWriter<byte> _writer;
        private readonly long _maximumBytes;
        private readonly Action<long> _setBytesWritten;
        private long _bytesWritten;

        internal BufferWriterStream(
            IBufferWriter<byte> writer,
            long maximumBytes,
            Action<long> setBytesWritten)
        {
            _writer = writer;
            _maximumBytes = maximumBytes;
            _setBytesWritten = setBytesWritten;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _bytesWritten;
        public override long Position
        {
            get => _bytesWritten;
            set => throw new NotSupportedException();
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > _maximumBytes - _bytesWritten)
                throw new MessagingFailFastException(
                    MessagingFailFastReason.OversizedPayload,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Payload exceeded the {0}-byte attachment threshold.",
                        _maximumBytes));

            buffer.CopyTo(_writer.GetSpan(buffer.Length));
            _writer.Advance(buffer.Length);
            _bytesWritten += buffer.Length;
            _setBytesWritten(_bytesWritten);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Write(buffer.AsSpan(offset, count));
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}
