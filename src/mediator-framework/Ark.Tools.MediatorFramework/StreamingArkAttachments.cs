// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Materializes metadata-delimited files from a client upload stream.</summary>
public static class StreamingArkAttachments
{
    /// <summary>
    /// Reads all metadata-delimited files from the stream, preserving their order.
    /// </summary>
    /// <param name="chunks">The upload chunks.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The uploaded attachments.</returns>
    public static async Task<IReadOnlyList<IArkAttachment>> ReadAllAsync(
        IAsyncEnumerable<UploadDocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        var attachments = new List<IArkAttachment>();
        List<ReadOnlyMemory<byte>>? content = null;
        UploadDocumentMetadata? metadata = null;

        await foreach (var chunk in chunks.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (chunk.Metadata is not null)
            {
                if (metadata is not null && content is not null)
                    attachments.Add(CreateAttachment(metadata, content));
                metadata = chunk.Metadata;
                content = [];
                continue;
            }

            if (metadata is null || chunk.Data is null)
                throw new InvalidOperationException("Upload chunks must start with metadata and contain data.");
            content!.Add(chunk.Data);
        }

        if (metadata is not null && content is not null)
            attachments.Add(CreateAttachment(metadata, content));
        return attachments;
    }

    private static IArkAttachment CreateAttachment(
        UploadDocumentMetadata metadata,
        List<ReadOnlyMemory<byte>> content)
    {
        return new ArkAttachment(
            metadata.Name,
            metadata.ContentType,
            () => new ChunkedReadStream(content));
    }

    private sealed class ChunkedReadStream : Stream
    {
        private readonly IReadOnlyList<ReadOnlyMemory<byte>> _segments;
        private readonly long _length;
        private long _position;
        private int _segmentIndex;
        private int _segmentOffset;

        public ChunkedReadStream(IReadOnlyList<ReadOnlyMemory<byte>> segments)
        {
            _segments = segments;
            _length = segments.Sum(static segment => (long)segment.Length);
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count)
            => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var copied = 0;
            while (copied < buffer.Length && _segmentIndex < _segments.Count)
            {
                var segment = _segments[_segmentIndex];
                if (_segmentOffset >= segment.Length)
                {
                    _segmentIndex++;
                    _segmentOffset = 0;
                    continue;
                }

                var amount = Math.Min(buffer.Length - copied, segment.Length - _segmentOffset);
                segment.Span.Slice(_segmentOffset, amount).CopyTo(buffer[copied..]);
                copied += amount;
                _position += amount;
                _segmentOffset += amount;
            }

            return copied;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            if (position < 0 || position > _length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            _position = position;
            _segmentIndex = 0;
            var remaining = position;
            while (_segmentIndex < _segments.Count && remaining > _segments[_segmentIndex].Length)
            {
                remaining -= _segments[_segmentIndex].Length;
                _segmentIndex++;
            }
            _segmentOffset = (int)remaining;
            return _position;
        }

        public override void Flush()
        {
            // No buffering occurs in this read-only stream.
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
