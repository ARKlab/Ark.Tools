// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Builds compressed or claim-checked payloads for a messaging transport.</summary>
public sealed class MessagingPayloadSender
{
    private readonly IMessagingDataBus _dataBus;
    private readonly MessagingNetworkOptions _network;
    private readonly CompressionAlgorithm _algorithm;
    private readonly int _compressionMinimumSizeBytes;

    /// <summary>Creates a payload sender.</summary>
    /// <param name="dataBus">The shared DataBus provider.</param>
    /// <param name="network">The network payload limits.</param>
    /// <param name="algorithm">The participant's sender-side compression algorithm.</param>
    /// <param name="compressionMinimumSizeBytes">The minimum size eligible for compression.</param>
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
    /// <typeparam name="T">The contract type.</typeparam>
    /// <param name="message">The contract value.</param>
    /// <param name="codec">The contract codec.</param>
    /// <param name="transport">The target transport.</param>
    /// <param name="headers">The mutable framework header map.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The inline payload, or an empty sequence when claim-check is used.</returns>
    public async Task<ReadOnlySequence<byte>> BuildOutgoingPayloadAsync<T>(
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
        _removeReservedHeader(headers, MessagingHeaders.ContentEncoding);
        _removeReservedHeader(headers, MessagingHeaders.PayloadAttachmentId);
        _removeReservedHeader(headers, MessagingHeaders.PayloadAttachmentLength);
        _removeReservedHeader(headers, MessagingHeaders.PayloadAttachmentSha256);
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new CompressionSwitchingBufferWriter(
            buffer,
            _algorithm,
            _compressionMinimumSizeBytes,
            Math.Max(
                _network.MaximumTransportPayloadBytes,
                _network.DataBusMaximumAttachmentBytes));
        codec.Serialize(message, writer);
        writer.Complete();

        if (writer.Compressed)
            _setReservedHeader(
                headers,
                MessagingHeaders.ContentEncoding,
                _algorithm == CompressionAlgorithm.Brotli ? "br" : "gzip");

        var payload = new ReadOnlySequence<byte>(buffer.WrittenMemory);
        var readOnlyHeaders = headers as IReadOnlyDictionary<string, string>
            ?? new ReadOnlyDictionary<string, string>(headers);
        var nativeSize = transport.MeasureNative(readOnlyHeaders, payload);
        var mustOffload = payload.Length > _network.MaximumTransportPayloadBytes
            || payload.Length > _network.DataBusOffloadThresholdBytes
            || (transport.MaximumInlineEnvelopeBytes is { } ceiling && nativeSize > ceiling);
        if (!mustOffload)
            return payload;

        if (payload.Length > _network.DataBusMaximumAttachmentBytes)
            throw new MessagingFailFastException(
                MessagingFailFastReason.OversizedPayload,
                "Payload exceeds the maximum DataBus attachment size.");

        var attachmentId = await _dataBus.StoreAsync(payload, ctk).ConfigureAwait(false);
        _setReservedHeader(headers, MessagingHeaders.PayloadAttachmentId, attachmentId);
        _setReservedHeader(
            headers,
            MessagingHeaders.PayloadAttachmentLength,
            payload.Length.ToString(CultureInfo.InvariantCulture));
        _setReservedHeader(headers, MessagingHeaders.PayloadAttachmentSha256, _sha256Hex(payload));

        if (transport.MaximumInlineEnvelopeBytes is { } attachmentCeiling
            && transport.MeasureNative(readOnlyHeaders, ReadOnlySequence<byte>.Empty) > attachmentCeiling)
        {
            throw new MessagingFailFastException(
                MessagingFailFastReason.OversizedHeaders,
                "Attachment-reference envelope exceeds the transport inline ceiling.");
        }

        return ReadOnlySequence<byte>.Empty;
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

    private static string _sha256Hex(in ReadOnlySequence<byte> payload)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var segment in payload)
            hash.AppendData(segment.Span);
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
