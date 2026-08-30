// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Security.Cryptography;

namespace Ark.Tools.MediatorFramework.Messaging;

internal sealed class HashingWriteStream : Stream
{
    private readonly Stream _inner;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private bool _completed;

    internal HashingWriteStream(Stream inner)
    {
        _inner = inner;
    }

    internal long _bytesWritten { get; private set; }

    internal string _completeHash()
    {
        if (_completed)
            throw new InvalidOperationException("The attachment hash has already been completed.");
        _completed = true;
        return Convert.ToHexString(_hash.GetHashAndReset());
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => !_completed;
    public override long Length => _bytesWritten;
    public override long Position
    {
        get => _bytesWritten;
        set => throw new NotSupportedException();
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        _inner.Write(buffer);
        _hash.AppendData(buffer);
        _bytesWritten += buffer.Length;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Write(buffer.AsSpan(offset, count));
    }

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _hash.AppendData(buffer.Span);
        _bytesWritten += buffer.Length;
    }

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        await WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override void Flush()
    {
        _inner.Flush();
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await _inner.FlushAsync(cancellationToken).ConfigureAwait(false);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
            _inner.Dispose();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        _hash.Dispose();
        await _inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
