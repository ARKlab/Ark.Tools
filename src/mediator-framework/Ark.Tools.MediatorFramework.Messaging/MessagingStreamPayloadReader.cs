// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.IO.Pipelines;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Adapts a prepared payload stream to generated asynchronous dispatch.</summary>
public sealed class MessagingStreamPayloadReader : IMessagingPayloadReader, IDisposable, IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<Stream>> _openStream;
    private readonly IMessagingCodec _codec;
    private Stream? _ownedStream;
    private bool _disposed;

    /// <summary>Creates a stream-backed payload reader.</summary>
    /// <param name="stream">The prepared payload stream owned by this reader.</param>
    /// <param name="codec">The codec used to deserialize contracts.</param>
    public MessagingStreamPayloadReader(Stream stream, IMessagingCodec codec)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _ownedStream = stream;
        _openStream = _openOwnedStream;
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    internal MessagingStreamPayloadReader(
        Func<CancellationToken, Task<Stream>> openStream,
        IMessagingCodec codec)
    {
        _openStream = openStream ?? throw new ArgumentNullException(nameof(openStream));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    /// <inheritdoc />
    public async Task<T> DeserializeAsync<T>(CancellationToken ctk) where T : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var stream = await _openStream(ctk).ConfigureAwait(false);
        var reader = PipeReader.Create(
            stream,
            new StreamPipeReaderOptions(leaveOpen: true));
        try
        {
            var result = await _codec.DeserializeAsync<T>(reader, ctk).ConfigureAwait(false);
            await reader.CopyToAsync(Stream.Null, ctk).ConfigureAwait(false);
            return result;
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
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _ownedStream?.Dispose();
        _ownedStream = null;
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_ownedStream is not null)
        {
            await _ownedStream.DisposeAsync().ConfigureAwait(false);
            _ownedStream = null;
        }
        GC.SuppressFinalize(this);
    }

    private Task<Stream> _openOwnedStream(CancellationToken ctk)
    {
        ctk.ThrowIfCancellationRequested();
        var stream = Interlocked.Exchange(ref _ownedStream, null)
            ?? throw new InvalidOperationException("The supplied payload stream cannot be replayed.");
        return Task.FromResult(stream);
    }
}
