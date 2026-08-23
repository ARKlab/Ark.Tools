# AZM-07 — Compression and shared DataBus claim-check

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-01, AZM-04, AZM-05
**Scope**: RUNTIME + TRANSPORT
**Design**: [DataBus claim-check](../../azure-functions-messaging-design.md#11-databus-claim-check), [Envelope and compatibility model](../../azure-functions-messaging-design.md#4-envelope-and-compatibility-model)

## Problem

Azure transport payload limits can reject valid application messages. The
runtime must compress first, then transparently offload the final compressed
bytes to a shared DataBus when they still exceed the configured limit.

## Execution map

- **Public API**: define DataBus provider/attachment abstractions and
  compression options in `Ark.Tools.MediatorFramework`.
- **Runtime**: implement gzip/Brotli, claim-check orchestration, integrity
  checks, and bounded reads in `Ark.Tools.MediatorFramework.Messaging`.
- **Provider seam**: use an opaque attachment ID; provider implementations own
  credentials, storage SDKs, and provider-specific minimum attachment
  lifetime. The concrete provider is a runtime composition decision, exactly
  like the transport; the network declares only offload/integrity thresholds.
  Include a first-class InMemory provider. Azure Blob is implemented by
  AZM-07A.
- **Order is fixed**: serialize → compress if eligible → threshold check →
  DataBus write; receive performs DataBus read → length/hash validation →
  bounded decompress → deserialize.
- **Mid-serialization compression switch (deferred from AZM-04)**: this task
  owns the sender-side automatic switch to compression during serialization —
  payload bytes below the participant's `CompressionMinimumSizeBytes`
  (generally small) buffer in pooled fixed-size arrays; when the counting
  writer crosses the minimum, the buffered prefix is re-piped into the
  compression writer and writing continues compressed. A fully streamed
  pipeline that can additionally divert to DataBus mid-write requires
  host-technology-aware pipe preparation and stays a recorded future host
  optimization ([future-improvements.md](../../future-improvements.md)), not
  part of this task.
- **Stop condition**: do not delete attachments during message settlement and
  do not add a durable outbox.

## Implementation steps

1. Define a transport-neutral DataBus abstraction equivalent to Rebus
   `IDataBus`, `DataBusAttachment`, and storage-management operations.
2. Support one runtime-composed provider used by every sender and consumer on
   the network regardless of the composed transport. All participants
   composing the
   same network must compose the same provider, store, and compatible options;
   this is a documented deployment assumption validated per participant.
3. Maximum payload and decompressed-size thresholds stay on the network
   (AZM-01); compression algorithm and minimum compression size are
   participant-owned sender-side settings (AZM-02).
   The network maximum payload threshold defaults to 240 000 bytes (safe for
   Service Bus standard tier). The runtime first constructs the complete
   inline envelope and offloads when either its compressed payload exceeds the
   configured threshold or the AZM-05 transport measurement exceeds its hard
   inline-envelope ceiling. Storage Queue measures its final canonical
   Base64 body, including headers and the poison-metadata reservation; it
   never decides from payload bytes alone. Startup warns when the configured
   threshold exceeds the composed transport's practical inline ceiling.
4. Implement gzip and Brotli content encodings selected per participant on the
   send side. Receive is header-driven and both encodings are always decodable
   by the runtime, so members may diverge freely — no cross-participant
   compression validation is needed.
5. Omit `amf1-content-encoding` for uncompressed payloads; emit `gzip` or `br`
   for compressed payloads.
6. Serialize and compress when eligible, then construct the complete inline
   envelope and measure its native representation. Store those exact
   compressed bytes in DataBus when the network payload threshold or the
   measured transport inline-envelope ceiling is exceeded. Re-measure the
   resulting attachment-reference envelope and fail explicitly if it cannot
   fit.
7. Emit `amf1-payload-attachment-id`, stored byte length, and SHA-256 metadata
   for transparent consumer retrieval and integrity validation.
8. Fetch attachments before decompression and deserialization. Missing,
   expired, or metadata-mismatched attachments must fail explicitly.
9. Keep deletion outside message consumption; provider lifecycle cleanup owns
   attachment lifecycle so retries, duplicate deliveries, and multiple event
   subscribers remain safe.
10. Add first-class InMemory DataBus storage with deterministic expiry driven
    by a test clock.
11. Put `MinimumAttachmentLifetime` on concrete provider composition, not the
    network. Validate it against bounded known windows (maximum scheduled
    delay plus retry/lock settings). Document that operators must additionally
    cover entity TTL, backlog, host outages, deployment delays, and outbox
    dwell time when the native SQL outbox is enlisted (AZM-14A), which the
    framework cannot prove. Document that a rolled-back enqueue transaction
    can leave an orphaned attachment that provider lifecycle cleanup
    eventually removes.

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed.

The shared DataBus provider seam (public API project, namespace
`Ark.MediatorFramework`). Attachment IDs are opaque; providers own credentials,
SDKs, and lifecycle:

```csharp
namespace Ark.MediatorFramework;

/// <summary>Shared DataBus provider seam (Rebus IDataBus equivalent). The concrete provider
/// is a runtime composition decision; every participant on a network composes the same
/// provider, store, and compatible options.</summary>
public interface IMessagingDataBus
{
    /// <summary>Stores the exact final (possibly compressed) payload bytes and returns an
    /// opaque attachment identifier.</summary>
    Task<string> StoreAsync(ReadOnlySequence<byte> content, CancellationToken ctk);

    /// <summary>Opens the attachment for bounded streaming read, verifying the stored byte
    /// length and SHA-256 digest. Missing, expired, or mismatched attachments throw
    /// MessagingFailFastException(AttachmentIntegrityFailure).</summary>
    Task<Stream> OpenReadAsync(string attachmentId, long expectedLength, string expectedSha256,
        CancellationToken ctk);
}
```

The send-side orchestration skeleton implementing the fixed order
serialize → compress if eligible → threshold check → DataBus write, including
the mid-serialization compression switch and the header emission rules
(`amf1-content-encoding` only when compressed; attachment id/length/sha256
only when offloaded):

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>Builds the outgoing payload bytes and payload-related headers for one send.</summary>
public sealed class MessagingPayloadSender
{
    private readonly IMessagingDataBus _dataBus;
    private readonly MessagingNetworkOptions _network;
    private readonly CompressionAlgorithm _algorithm;    // participant sender-side setting (AZM-02)
    private readonly int _compressionMinimumSizeBytes;   // participant sender-side setting (AZM-02)

    /// <summary>Returns the final payload for the transport message and mutates headers.</summary>
    public async Task<ReadOnlySequence<byte>> BuildOutgoingPayloadAsync<T>(
        T message, IMessagingCodec codec, IMessagingTransport transport,
        Dictionary<string, string> headers, CancellationToken ctk) where T : class
    {
        headers[MessagingHeaders.ContentType] = codec.ContentType;

        // 1. Serialize through a counting + switching writer. Bytes below
        //    _compressionMinimumSizeBytes buffer into pooled fixed-size arrays; when the
        //    running count crosses the minimum, the buffered prefix is re-piped into a
        //    BrotliStream/GZipStream over the buffer writer and writing continues
        //    compressed (the mid-serialization switch). The counter throws
        //    MessagingFailFastException(OversizedPayload) past
        //    _network.MaximumTransportPayloadBytes.
        var buffer = new ArrayBufferWriter<byte>();        // transport-owned buffered form
        var writer = new CompressionSwitchingBufferWriter(
            buffer, _algorithm, _compressionMinimumSizeBytes,
            _network.MaximumTransportPayloadBytes);
        codec.Serialize(message, writer);
        writer.Complete();                                 // flush final compressor frame

        if (writer.Compressed)
            headers[MessagingHeaders.ContentEncoding] =
                _algorithm == CompressionAlgorithm.Brotli ? "br" : "gzip";
        // The header is omitted entirely for uncompressed payloads.

        var payload = new ReadOnlySequence<byte>(buffer.WrittenMemory);

        // 2. Threshold check on the FINAL compressed bytes plus the measured complete
        //    native envelope (headers and transport encoding included, AZM-05 seam).
        var native = transport.MeasureNative(headers, payload);
        var mustOffload = payload.Length > _network.DataBusOffloadThresholdBytes
            || (transport.MaximumInlineEnvelopeBytes is { } ceiling && native > ceiling);
        if (!mustOffload)
            return payload;

        if (payload.Length > _network.DataBusMaximumAttachmentBytes)
            throw new MessagingFailFastException(MessagingFailFastReason.OversizedPayload,
                "Payload exceeds the maximum DataBus attachment size.");

        // 3. DataBus write of those exact compressed bytes; emit attachment headers.
        var attachmentId = await _dataBus.StoreAsync(payload, ctk).ConfigureAwait(false);
        headers[MessagingHeaders.PayloadAttachmentId] = attachmentId;
        headers[MessagingHeaders.PayloadAttachmentLength] =
            payload.Length.ToString(CultureInfo.InvariantCulture);
        headers[MessagingHeaders.PayloadAttachmentSha256] =
            _sha256Hex(payload);   // IncrementalHash over the sequence segments; no byte[]

        // 4. Re-measure the attachment-reference envelope and fail explicitly if it
        //    still cannot fit the transport's hard inline ceiling.
        var reference = ReadOnlySequence<byte>.Empty;
        if (transport.MaximumInlineEnvelopeBytes is { } c
            && transport.MeasureNative(headers, reference) > c)
            throw new MessagingFailFastException(MessagingFailFastReason.OversizedHeaders,
                "Attachment-reference envelope exceeds the transport inline ceiling.");
        return reference;
    }
}
```

The receive-side skeleton — attachment fetch with length/SHA-256 verification,
then bounded decompression; runs in the header phase before the generated
typed binder:

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>Prepares the payload source for the typed phase: DataBus fetch → integrity
/// validation → bounded decompress. Read behavior is header-driven only.</summary>
public sealed class MessagingPayloadReceiver
{
    private readonly IMessagingDataBus _dataBus;
    private readonly MessagingNetworkOptions _network;

    public async Task<ReadOnlySequence<byte>> PreparePayloadAsync(
        IReadOnlyDictionary<string, string> headers, ReadOnlySequence<byte> transportPayload,
        CancellationToken ctk)
    {
        var payload = transportPayload;

        if (headers.TryGetValue(MessagingHeaders.PayloadAttachmentId, out var attachmentId))
        {
            var expectedLength = long.Parse(
                headers[MessagingHeaders.PayloadAttachmentLength], CultureInfo.InvariantCulture);
            var expectedSha256 = headers[MessagingHeaders.PayloadAttachmentSha256];

            // The provider verifies length and SHA-256 while streaming; missing, expired,
            // or mismatched attachments throw
            // MessagingFailFastException(AttachmentIntegrityFailure).
            var stream = await _dataBus
                .OpenReadAsync(attachmentId, expectedLength, expectedSha256, ctk)
                .ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                payload = await _bufferAsync(stream, ctk).ConfigureAwait(false);
            }
        }

        if (headers.TryGetValue(MessagingHeaders.ContentEncoding, out var encoding))
        {
            payload = encoding switch
            {
                "gzip" => _decompressBounded(payload, useBrotli: false),
                "br" => _decompressBounded(payload, useBrotli: true),
                _ => throw new MessagingFailFastException(
                    MessagingFailFastReason.UnsupportedContentEncoding, encoding),
            };
        }

        return payload;   // handed to the generated binder through IMessagingPayloadReader
    }

    private ReadOnlySequence<byte> _decompressBounded(
        ReadOnlySequence<byte> compressed, bool useBrotli)
    {
        // Bounded decompression reader over GZipStream/BrotliStream: counts output bytes
        // while reading and throws MessagingFailFastException(OversizedPayload) as soon as
        // _network.MaximumDecompressedPayloadBytes is exceeded — never buffers past the
        // bound and never trusts the compressed length.
        // ...
    }
}
```

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with the
serialize-compress-threshold-claim-check order, provider-specific lifetime
responsibility, per-participant sender-side compression with header-driven
reads, and the network-wide provider/store compatibility requirement.

## Sample extension

Extend the Book sample with a large background payload fixture that exercises
compression and DataBus claim-check over the InMemory transport. Azure
transport coverage lands with AZM-10/AZM-11.

## Required test coverage

- Gzip and Brotli compression/decompression.
- Minimum-size threshold and uncompressed encoding-header behavior.
- DataBus offload after compression when either the final payload threshold or
  the measured complete inline envelope exceeds its limit, including
  header/encoding boundaries.
- Claim-check envelope references survive the InMemory transport round trip.
- Transparent consumer retrieval and decompression.
- Missing, expired, and metadata-mismatched attachment failures.
- Length and SHA-256 mismatch failures.
- Shared attachment remains readable across retry and two subscribers.
- Retention cleanup is external to consumer settlement.
- Provider minimum lifetime validation covers bounded scheduling/retry values.
- Documentation includes entity TTL, backlog, outage, deployment-delay, and
  outbox dwell-time lifetime considerations, plus rollback orphan cleanup.

## Outcomes

- Large messages work consistently on every transport.
- Compression reduces payload size before the claim-check decision.
- Consumers do not need application-level DataBus code.

## Acceptance

- [x] Gzip and Brotli are implemented behind participant configuration, with
  header-driven reads that always decode both.
- [x] Final compressed bytes, not original bytes, determine DataBus offload;
  the complete inline envelope is also measured so headers and transport
  encoding cannot exceed a transport limit.
- [x] Claim-check is transport-neutral and proven over InMemory.
- [x] Consumers retrieve, validate, decompress, and deserialize transparently.
- [x] Provider lifecycle cleanup, not consumers, owns deletion.
- [x] The [task board](../README.md) status for AZM-07 is updated to this task's acceptance state.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
