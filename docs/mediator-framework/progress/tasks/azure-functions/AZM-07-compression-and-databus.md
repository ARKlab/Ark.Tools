# AZM-07 — Compression and shared DataBus claim-check

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-01, AZM-04, AZM-05
**Scope**: RUNTIME + TRANSPORT
**Design**: [DataBus claim-check](../../azure-functions-messaging-design.md#11-databus-claim-check), [Wire metadata and compatibility model](../../azure-functions-messaging-design.md#4-envelope-and-compatibility-model)

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
- **Serde boundary**: preserve AZM-04's generated generic contract binding:
  codecs write to `IBufferWriter<byte>` and read `ReadOnlySequence<byte>`.
  Compression operates only on the resulting body bytes, never headers or
  CLR types. This is the same closed-generic boundary used by generated Minimal
  API and HttpTrigger parameter binding and response serialization.
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
   Service Bus standard tier). The runtime keeps context headers separate from
   the body and offloads when either its compressed body exceeds the
   configured threshold or the AZM-05 transport measurement exceeds its hard
   inline-message ceiling. Storage Queue measures its final transport-owned
   Base64 body, including headers and the poison-metadata reservation; it
   never decides from payload bytes alone. Startup warns when the configured
   threshold exceeds the composed transport's practical inline ceiling.
4. Implement gzip and Brotli content encodings selected per participant on the
   send side. Receive is header-driven and both encodings are always decodable
   by the runtime, so members may diverge freely — no cross-participant
   compression validation is needed.
5. Omit `amf1-content-encoding` for uncompressed payloads; emit `gzip` or `br`
   for compressed payloads.
6. Serialize and compress when eligible, then have the transport measure the
   complete native representation of the separate headers and body. Store those exact
   compressed bytes in DataBus when the network payload threshold or the
   measured transport inline-message ceiling is exceeded. Re-measure the
   resulting attachment-reference message and fail explicitly if it cannot
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
  the measured complete inline message exceeds its limit, including
  header/encoding boundaries.
- Claim-check message references survive the InMemory transport round trip.
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

- [ ] Gzip and Brotli are implemented behind participant configuration, with
  header-driven reads that always decode both.
- [ ] Final compressed bytes, not original bytes, determine DataBus offload;
  the complete inline message is also measured so headers and transport
  encoding cannot exceed a transport limit.
- [ ] Claim-check is transport-neutral and proven over InMemory.
- [ ] Consumers retrieve, validate, decompress, and deserialize transparently.
- [ ] Provider lifecycle cleanup, not consumers, owns deletion.
- [ ] The [task board](../README.md) status for AZM-07 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
