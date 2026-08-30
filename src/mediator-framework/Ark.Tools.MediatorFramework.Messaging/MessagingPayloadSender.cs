// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.IO.Pipelines;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Builds compressed or claim-checked payloads for a messaging transport.</summary>
public sealed class MessagingPayloadSender
{
    private readonly IMessagingDataBus _dataBus;
    private readonly MessagingNetworkOptions _network;
    private readonly CompressionAlgorithm _algorithm;
    private readonly int _compressionMinimumSizeBytes;

    /// <summary>Creates a payload sender.</summary>
    public MessagingPayloadSender(
        IMessagingDataBus dataBus,
        MessagingNetworkOptions network,
        CompressionAlgorithm algorithm,
        int compressionMinimumSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(dataBus);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentOutOfRangeException.ThrowIfNegative(compressionMinimumSizeBytes);
        if (network.DataBusMaximumAttachmentBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(network), "The maximum attachment size must be positive.");

        _dataBus = dataBus;
        _network = network;
        _algorithm = algorithm;
        _compressionMinimumSizeBytes = compressionMinimumSizeBytes;
    }

    /// <summary>Serializes, optionally compresses, and claim-checks a message.</summary>
    public async Task<MessagingOutgoingPayload> BuildOutgoingPayloadAsync<T>(
        T message,
        IMessagingCodec codec,
        IMessagingTransport transport,
        IDictionary<string, string> headers,
        CancellationToken ctk)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(headers);
        ctk.ThrowIfCancellationRequested();

        _setReservedHeader(headers, MessagingHeaders.ContentType, codec.ContentType);
        _removePayloadHeaders(headers);
        if (_algorithm != CompressionAlgorithm.None)
        {
            _setReservedHeader(
                headers,
                MessagingHeaders.ContentEncoding,
                _algorithm == CompressionAlgorithm.Brotli ? "br" : "gzip");
        }
        var readOnlyHeaders = headers as IReadOnlyDictionary<string, string>
            ?? new ReadOnlyDictionary<string, string>(headers);
        var configuredInlineLimit = Math.Min(
            _network.MaximumTransportPayloadBytes,
            _network.DataBusOffloadThresholdBytes);
        var transportInlineLimit = transport.GetMaximumInlinePayloadBytes(readOnlyHeaders);
        var inlineLimit = checked((int)Math.Min(
            configuredInlineLimit,
            transportInlineLimit ?? int.MaxValue));

        var serialization = new Pipe(_pipeOptions());
        var finalPayload = new Pipe(_pipeOptions());
        var serializeTask = _serializeAsync(codec, message, serialization.Writer, ctk);
        var compressTask = _compressAsync(serialization.Reader, finalPayload.Writer, ctk);
        var destinationTask = _writeDestinationAsync(
            finalPayload.Reader,
            inlineLimit,
            transport,
            readOnlyHeaders,
            ctk);

        try
        {
            await Task.WhenAll(serializeTask, compressTask, destinationTask).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (destinationTask.IsCompletedSuccessfully)
            {
                var completedPayload = await destinationTask.ConfigureAwait(false);
                try
                {
                    await _deleteAttachmentAsync(completedPayload).ConfigureAwait(false);
                }
                catch (Exception cleanupException) when (!_isCriticalException(cleanupException))
                {
                    throw new AggregateException(exception, cleanupException);
                }
                finally
                {
                    completedPayload.Dispose();
                }
            }
            throw;
        }
        var result = await destinationTask.ConfigureAwait(false);
        if (result.Attachment is { } attachment)
        {
            _setReservedHeader(headers, MessagingHeaders.PayloadAttachmentId, attachment.Id);
            _setReservedHeader(
                headers,
                MessagingHeaders.PayloadAttachmentLength,
                attachment.Length.ToString(CultureInfo.InvariantCulture));
            _setReservedHeader(headers, MessagingHeaders.PayloadAttachmentSha256, attachment.Sha256);
            if (transport.MaximumInlineEnvelopeBytes is { } ceiling
                && transport.MeasureNative(readOnlyHeaders, ReadOnlySequence<byte>.Empty) > ceiling)
            {
                result.Dispose();
                var exception = new MessagingFailFastException(
                    MessagingFailFastReason.OversizedHeaders,
                    "Attachment-reference envelope exceeds the transport inline ceiling.");
                try
                {
                    await _deleteAttachmentAsync(result).ConfigureAwait(false);
                }
                catch (Exception cleanupException) when (!_isCriticalException(cleanupException))
                {
                    throw new AggregateException(exception, cleanupException);
                }
                throw exception;
            }
        }

        var compressed = await compressTask.ConfigureAwait(false);
        if (!compressed)
            _removeReservedHeader(headers, MessagingHeaders.ContentEncoding);

        return result;
    }

    private async Task _deleteAttachmentAsync(MessagingOutgoingPayload payload)
    {
        if (payload.Attachment is { } attachment)
            await _dataBus.DeleteAsync(attachment.Id, CancellationToken.None).ConfigureAwait(false);
    }

    private static bool _isCriticalException(Exception exception)
    {
        return exception is OutOfMemoryException
            or StackOverflowException
            or AccessViolationException;
    }

    private static async Task _serializeAsync<T>(
        IMessagingCodec codec,
        T message,
        PipeWriter writer,
        CancellationToken ctk)
        where T : class
    {
        Exception? failure = null;
        try
        {
            await codec.SerializeAsync(message, writer, ctk).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            await writer.CompleteAsync(failure).ConfigureAwait(false);
        }
    }

    private async Task<bool> _compressAsync(PipeReader input, PipeWriter output, CancellationToken ctk)
    {
        Exception? failure = null;
        var prefix = ArrayPool<byte>.Shared.Rent(Math.Max(_compressionMinimumSizeBytes, 1));
        var prefixLength = 0;
        Stream? compressor = null;
        var compressed = false;
        try
        {
            while (true)
            {
                var result = await input.ReadAsync(ctk).ConfigureAwait(false);
                var buffer = result.Buffer;
                foreach (var segment in buffer)
                {
                    var remaining = segment;
                    if (compressor is null && _algorithm != CompressionAlgorithm.None)
                    {
                        var copy = Math.Min(
                            remaining.Length,
                            _compressionMinimumSizeBytes - prefixLength);
                        remaining.Span[..copy].CopyTo(prefix.AsSpan(prefixLength));
                        prefixLength += copy;
                        remaining = remaining[copy..];
                        if (prefixLength >= _compressionMinimumSizeBytes)
                        {
#pragma warning disable CA2000 // The stream is disposed in this method's finally block.
                            compressor = _createCompressionStream(output);
#pragma warning restore CA2000
                            compressed = true;
                            await compressor.WriteAsync(prefix.AsMemory(0, prefixLength), ctk)
                                .ConfigureAwait(false);
                        }
                    }

                    if (remaining.IsEmpty)
                        continue;
                    if (compressor is not null)
                        await compressor.WriteAsync(remaining, ctk).ConfigureAwait(false);
                    else
                        await output.WriteAsync(remaining, ctk).ConfigureAwait(false);
                }

                input.AdvanceTo(buffer.End);
                if (result.IsCompleted)
                    break;
            }

            if (compressor is null && prefixLength > 0)
                await output.WriteAsync(prefix.AsMemory(0, prefixLength), ctk).ConfigureAwait(false);
            if (compressor is not null)
            {
                await compressor.DisposeAsync().ConfigureAwait(false);
                compressor = null;
            }
            return compressed;
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            Array.Clear(prefix, 0, prefixLength);
            ArrayPool<byte>.Shared.Return(prefix);
            if (compressor is not null)
                await compressor.DisposeAsync().ConfigureAwait(false);
            await input.CompleteAsync(failure).ConfigureAwait(false);
            await output.CompleteAsync(failure).ConfigureAwait(false);
        }
    }

    private async Task<MessagingOutgoingPayload> _writeDestinationAsync(
        PipeReader input,
        int inlineLimit,
        IMessagingTransport transport,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken ctk)
    {
        var rented = ArrayPool<byte>.Shared.Rent(Math.Max(inlineLimit, 1));
        var buffered = 0;
        long total = 0;
        IMessagingDataBusWriteSession? session = null;
        Exception? failure = null;
        try
        {
            while (true)
            {
                var result = await input.ReadAsync(ctk).ConfigureAwait(false);
                var source = result.Buffer;
                foreach (var segment in source)
                {
                    total += segment.Length;
                    if (session is not null && total > _network.DataBusMaximumAttachmentBytes)
                        throw _oversized();
                    if (session is null && segment.Length <= inlineLimit - buffered)
                    {
                        segment.Span.CopyTo(rented.AsSpan(buffered));
                        buffered += segment.Length;
                        continue;
                    }

                    session ??= await _openDataBusAsync(rented, buffered, ctk).ConfigureAwait(false);
                    if (total > _network.DataBusMaximumAttachmentBytes)
                        throw _oversized();
                    await session.Stream.WriteAsync(segment, ctk).ConfigureAwait(false);
                }

                input.AdvanceTo(source.End);
                if (result.IsCompleted)
                    break;
            }

            if (session is null)
            {
                var payload = new ReadOnlySequence<byte>(rented.AsMemory(0, buffered));
                if (transport.MaximumInlineEnvelopeBytes is not { } ceiling
                    || transport.MeasureNative(headers, payload) <= ceiling)
                {
                    return new MessagingOutgoingPayload(rented, buffered, attachment: null);
                }

                session = await _openDataBusAsync(rented, buffered, ctk).ConfigureAwait(false);
            }

            var attachment = await session.CompleteAsync(ctk).ConfigureAwait(false);
            await session.DisposeAsync().ConfigureAwait(false);
            session = null;
            Array.Clear(rented, 0, buffered);
            ArrayPool<byte>.Shared.Return(rented);
            return new MessagingOutgoingPayload(null, 0, attachment);
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            await input.CompleteAsync(failure).ConfigureAwait(false);
            if (session is not null)
                await session.DisposeAsync().ConfigureAwait(false);
            if (failure is not null)
            {
                Array.Clear(rented, 0, buffered);
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private async Task<IMessagingDataBusWriteSession> _openDataBusAsync(
        byte[] prefix,
        int prefixLength,
        CancellationToken ctk)
    {
        var session = await _dataBus.OpenWriteAsync(ctk).ConfigureAwait(false);
        try
        {
            if (prefixLength > 0)
                await session.Stream.WriteAsync(prefix.AsMemory(0, prefixLength), ctk).ConfigureAwait(false);
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private Stream _createCompressionStream(PipeWriter output)
    {
        var stream = output.AsStream(leaveOpen: true);
        return _algorithm switch
        {
            CompressionAlgorithm.Gzip => new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: false),
            CompressionAlgorithm.Brotli => new BrotliStream(stream, CompressionLevel.Fastest, leaveOpen: false),
            _ => throw new InvalidOperationException("Compression is not configured.")
        };
    }

    private static PipeOptions _pipeOptions()
    {
        return new PipeOptions(
            pool: MemoryPool<byte>.Shared,
            pauseWriterThreshold: 65_536,
            resumeWriterThreshold: 32_768,
            useSynchronizationContext: false);
    }

    private static void _removePayloadHeaders(IDictionary<string, string> headers)
    {
        _removeReservedHeader(headers, MessagingHeaders.ContentEncoding);
        _removeReservedHeader(headers, MessagingHeaders.PayloadAttachmentId);
        _removeReservedHeader(headers, MessagingHeaders.PayloadAttachmentLength);
        _removeReservedHeader(headers, MessagingHeaders.PayloadAttachmentSha256);
    }

    private MessagingFailFastException _oversized()
    {
        return new MessagingFailFastException(
            MessagingFailFastReason.OversizedPayload,
            string.Format(
                CultureInfo.InvariantCulture,
                "Payload exceeded the {0}-byte attachment threshold.",
                _network.DataBusMaximumAttachmentBytes));
    }

    private static void _setReservedHeader(
        IDictionary<string, string> headers,
        string key,
        string value)
    {
        if (headers is IMessagingFrameworkHeaders frameworkHeaders)
            frameworkHeaders.SetReserved(key, value);
        else
            headers[key] = value;
    }

    private static void _removeReservedHeader(IDictionary<string, string> headers, string key)
    {
        if (headers is IMessagingFrameworkHeaders frameworkHeaders)
            frameworkHeaders.RemoveReserved(key);
        else
            headers.Remove(key);
    }
}

/// <summary>Owns an inline pooled payload or identifies a committed DataBus attachment.</summary>
public sealed class MessagingOutgoingPayload : IDisposable
{
    private byte[]? _buffer;

    internal MessagingOutgoingPayload(
        byte[]? buffer,
        int length,
        MessagingDataBusAttachment? attachment)
    {
        _buffer = buffer;
        _length = length;
        Attachment = attachment;
    }

    /// <summary>Gets the inline payload, or an empty sequence when DataBus is used.</summary>
    public ReadOnlySequence<byte> Sequence =>
        _buffer is null ? ReadOnlySequence<byte>.Empty : new ReadOnlySequence<byte>(_buffer.AsMemory(0, _length));

    /// <summary>Gets the inline payload length.</summary>
    public long Length => _length;

    /// <summary>Gets whether the inline payload is empty.</summary>
    public bool IsEmpty => _length == 0;

    /// <summary>Gets the committed attachment metadata when DataBus is used.</summary>
    public MessagingDataBusAttachment? Attachment { get; }

    private int _length { get; }

    /// <summary>Returns the owned inline sequence for transport APIs.</summary>
    public ReadOnlySequence<byte> ToReadOnlySequence()
    {
        return Sequence;
    }

    /// <summary>Returns the owned inline sequence for transport APIs.</summary>
    public static implicit operator ReadOnlySequence<byte>(MessagingOutgoingPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return payload.Sequence;
    }
    /// <inheritdoc />
    public void Dispose()
    {
        var buffer = Interlocked.Exchange(ref _buffer, null);
        if (buffer is not null)
        {
            Array.Clear(buffer, 0, _length);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
