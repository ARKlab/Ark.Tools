// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>
/// Exposes a metadata-first sequence of upload chunks as a forward-only attachment stream.
/// </summary>
public sealed class StreamingArkAttachment : IArkAttachment
{
    private readonly IAsyncEnumerable<UploadDocumentChunk> _chunks;

    /// <summary>Initializes a new instance of the <see cref="StreamingArkAttachment"/> class.</summary>
    /// <param name="chunks">The metadata-first upload chunk sequence.</param>
    public StreamingArkAttachment(IAsyncEnumerable<UploadDocumentChunk> chunks)
    {
        _chunks = chunks ?? throw new ArgumentNullException(nameof(chunks));
    }

    /// <inheritdoc />
    public string Name { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string ContentType { get; private set; } = string.Empty;

    /// <inheritdoc />
    public Stream OpenRead()
    {
        return new UploadChunkStream(_chunks, this);
    }

    private sealed class UploadChunkStream : Stream
    {
        private readonly IAsyncEnumerator<UploadDocumentChunk> _enumerator;
        private readonly StreamingArkAttachment _attachment;
        private byte[] _buffer = [];
        private int _offset;
        private bool _started;
        private bool _completed;

        public UploadChunkStream(IAsyncEnumerable<UploadDocumentChunk> chunks, StreamingArkAttachment attachment)
        {
            _enumerator = chunks.GetAsyncEnumerator();
            _attachment = attachment;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask DisposeAsync()
        {
            await _enumerator.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Synchronous reads are not supported for streamed uploads.");
        }

        public override int Read(Span<byte> buffer)
        {
            throw new NotSupportedException("Synchronous reads are not supported for streamed uploads.");
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0)
                return 0;

            while (_offset == _buffer.Length && !_completed)
            {
                await ReadNextChunkAsync(cancellationToken).ConfigureAwait(false);
            }

            var count = Math.Min(buffer.Length, _buffer.Length - _offset);
            _buffer.AsMemory(_offset, count).CopyTo(buffer);
            _offset += count;
            return count;
        }

        private async ValueTask ReadNextChunkAsync(CancellationToken cancellationToken)
        {
            if (!await _enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                _completed = true;
                return;
            }

            var chunk = _enumerator.Current;
            if (!_started)
            {
                if (chunk.Metadata is null)
                    throw new InvalidOperationException("The first upload chunk must contain metadata.");

                _attachment.Name = ArkAttachmentName.Sanitize(chunk.Metadata.Name);
                _attachment.ContentType = chunk.Metadata.ContentType;
                _started = true;
                _buffer = [];
                _offset = 0;
                return;
            }

            if (chunk.Metadata is not null || chunk.Data is null)
                throw new InvalidOperationException("Upload chunks after metadata must contain data.");

            _buffer = chunk.Data;
            _offset = 0;
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
