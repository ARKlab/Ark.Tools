// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Security.Cryptography;

using NodaTime;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>In-memory DataBus provider with clock-controlled attachment expiry.</summary>
public sealed class InMemoryMessagingDataBus : IMessagingDataBus
{
    private readonly ConcurrentDictionary<string, Attachment> _attachments = new(StringComparer.Ordinal);
    private readonly IClock _clock;
    private readonly Duration _lifetime;

    /// <summary>Creates a provider using the system clock and a one-hour lifetime.</summary>
    public InMemoryMessagingDataBus()
        : this(SystemClock.Instance, Duration.FromHours(1))
    {
    }

    /// <summary>Creates a provider with a supplied clock and attachment lifetime.</summary>
    /// <param name="clock">The clock used for expiry.</param>
    /// <param name="lifetime">The attachment lifetime.</param>
    public InMemoryMessagingDataBus(IClock clock, Duration lifetime)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (lifetime <= Duration.Zero)
            throw new ArgumentOutOfRangeException(nameof(lifetime), "The attachment lifetime must be positive.");

        _clock = clock;
        _lifetime = lifetime;
    }

    /// <summary>Gets the configured minimum attachment lifetime.</summary>
    public Duration MinimumAttachmentLifetime => _lifetime;

    /// <summary>Gets the number of currently stored, unexpired attachments.</summary>
    public int Count
    {
        get
        {
            _removeExpired();
            return _attachments.Count;
        }
    }

    /// <inheritdoc />
    public Task<string> StoreAsync(ReadOnlySequence<byte> content, CancellationToken ctk)
    {
        ctk.ThrowIfCancellationRequested();
        _removeExpired();
        var bytes = content.ToArray();
        var id = Guid.NewGuid().ToString("N");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        _attachments[id] = new Attachment(
            bytes,
            hash,
            _clock.GetCurrentInstant() + _lifetime);
        return Task.FromResult(id);
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(
        string attachmentId,
        long expectedLength,
        string expectedSha256,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(attachmentId);
        ArgumentException.ThrowIfNullOrEmpty(expectedSha256);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedLength);
        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(expectedSha256);
        }
        catch (FormatException)
        {
            expectedHash = [];
        }

        if (expectedHash.Length != 32)
        {
            throw new MessagingFailFastException(
                MessagingFailFastReason.AttachmentIntegrityFailure,
                "The payload attachment SHA-256 digest is invalid.");
        }

        ctk.ThrowIfCancellationRequested();
        if (!_attachments.TryGetValue(attachmentId, out var attachment)
            || attachment.ExpiresAt <= _clock.GetCurrentInstant())
        {
            _attachments.TryRemove(attachmentId, out _);
            throw new MessagingFailFastException(
                MessagingFailFastReason.AttachmentIntegrityFailure,
                "The payload attachment is missing or expired.");
        }

        if (attachment.Content.LongLength != expectedLength
            || !string.Equals(attachment.Sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new MessagingFailFastException(
                MessagingFailFastReason.AttachmentIntegrityFailure,
                "The payload attachment metadata does not match the envelope.");
        }

        Stream stream = new Sha256ValidatingReadStream(
            new MemoryStream(attachment.Content, writable: false),
            expectedLength,
            expectedSha256);
        return Task.FromResult(stream);
    }

    /// <summary>Removes expired attachments using the configured clock.</summary>
    public void RemoveExpired()
    {
        _removeExpired();
    }

    private void _removeExpired()
    {
        var now = _clock.GetCurrentInstant();
        foreach (var pair in _attachments)
        {
            if (pair.Value.ExpiresAt <= now)
                _attachments.TryRemove(pair.Key, out _);
        }
    }

    private sealed record Attachment(byte[] Content, string Sha256, Instant ExpiresAt);
}

internal sealed class Sha256ValidatingReadStream : Stream
{
    private readonly Stream _inner;
    private readonly long _expectedLength;
    private readonly string _expectedSha256;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private long _read;
    private bool _validated;

    internal Sha256ValidatingReadStream(Stream inner, long expectedLength, string expectedSha256)
    {
        _inner = inner;
        _expectedLength = expectedLength;
        _expectedSha256 = expectedSha256;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _expectedLength;
    public override long Position
    {
        get => _read;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0)
            return 0;

        var read = _inner.Read(buffer, offset, count);
        _validate(buffer.AsSpan(offset, read), read == 0);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty)
            return 0;

        var read = _inner.Read(buffer);
        _validate(buffer[..read], read == 0);
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
            return 0;

        var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        _validate(buffer[..read].Span, read == 0);
        return read;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        if (count == 0)
            return 0;

        var read = await _inner.ReadAsync(
            buffer.AsMemory(offset, count),
            cancellationToken).ConfigureAwait(false);
        _validate(buffer.AsSpan(offset, read), read == 0);
        return read;
    }

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
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

    private void _validate(ReadOnlySpan<byte> bytes, bool endOfStream)
    {
        if (_validated)
            return;

        byte[] expectedHash;
        _hash.AppendData(bytes);
        _read += bytes.Length;
        if (_read > _expectedLength)
            _fail("The payload attachment is longer than its envelope metadata.");
        if (!endOfStream)
            return;

        try
        {
            expectedHash = Convert.FromHexString(_expectedSha256);
        }
        catch (FormatException)
        {
            _fail("The payload attachment SHA-256 digest is invalid.");
            return;
        }

        if (_read != _expectedLength
            || !CryptographicOperations.FixedTimeEquals(expectedHash, _hash.GetHashAndReset()))
        {
            _fail("The payload attachment SHA-256 digest does not match its envelope metadata.");
        }

        _validated = true;
    }

    private static void _fail(string message)
    {
        throw new MessagingFailFastException(
            MessagingFailFastReason.AttachmentIntegrityFailure,
            message);
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
}
