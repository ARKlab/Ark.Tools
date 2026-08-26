// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Adapts a prepared payload stream to the generated sequence-based reader contract.</summary>
public sealed class MessagingStreamPayloadReader : IMessagingPayloadReader, IDisposable, IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly IMessagingCodec _codec;
    private ReadOnlySequence<byte>? _payload;
    private bool _disposed;

    /// <summary>Creates a stream-backed payload reader.</summary>
    /// <param name="stream">The prepared payload stream owned by this reader.</param>
    /// <param name="codec">The codec used to deserialize contracts.</param>
    public MessagingStreamPayloadReader(Stream stream, IMessagingCodec codec)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    /// <inheritdoc />
    public ReadOnlySequence<byte> ReadPayload()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_payload is not { } payload)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var span = buffer.GetSpan(8_192);
            while (true)
            {
                var read = _stream.Read(span);
                if (read == 0)
                    break;
                buffer.Advance(read);
                span = buffer.GetSpan(8_192);
            }

            payload = new ReadOnlySequence<byte>(buffer.WrittenMemory);
            _payload = payload;
        }

        return payload;
    }

    /// <inheritdoc />
    public T Deserialize<T>() where T : class
    {
        var payload = ReadPayload();
        try
        {
            return _codec.Deserialize<T>(payload);
        }
        catch (MessagingFailFastException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new MessagingFailFastException(
                MessagingFailFastReason.MalformedPayload,
                exception.Message,
                exception);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _stream.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
