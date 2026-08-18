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
- **Stop condition**: do not delete attachments during message settlement and
  do not add a durable outbox.

## Implementation steps

1. Define a transport-neutral DataBus abstraction equivalent to Rebus
   `IDataBus`, `DataBusAttachment`, and storage-management operations.
2. Support one runtime-composed provider used by every sender and consumer on
   the network regardless of the composed transport. All hosts composing the
   same network must compose the same provider, store, and compatible options;
   this is a documented deployment assumption validated per host.
3. Add network-configured maximum payload and minimum compression-size settings,
   with defaults derived from current Azure transport limitations.
4. Implement gzip and Brotli content encodings selected by network configuration.
5. Omit `amf1-content-encoding` for uncompressed payloads; emit `gzip` or `br`
   for compressed payloads.
6. Serialize, compress when eligible, compare final bytes with the transport
   threshold, and store those exact compressed bytes in DataBus when needed.
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
    cover entity TTL, backlog, host outages, and deployment delays, which the
    framework cannot prove.

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with the
serialize-compress-threshold-claim-check order, provider-specific lifetime
responsibility, and the network-wide provider/store compatibility requirement.

## Sample extension

Extend the Book sample with a large background payload fixture that exercises
compression and DataBus claim-check over the InMemory transport. Azure
transport coverage lands with AZM-10/AZM-11.

## Required test coverage

- Gzip and Brotli compression/decompression.
- Minimum-size threshold and uncompressed encoding-header behavior.
- DataBus offload after compression when the final bytes exceed the limit.
- Claim-check envelope references survive the InMemory transport round trip.
- Transparent consumer retrieval and decompression.
- Missing, expired, and metadata-mismatched attachment failures.
- Length and SHA-256 mismatch failures.
- Shared attachment remains readable across retry and two subscribers.
- Retention cleanup is external to consumer settlement.
- Provider minimum lifetime validation covers bounded scheduling/retry values.
- Documentation includes entity TTL, backlog, outage, and deployment-delay
  lifetime considerations.

## Outcomes

- Large messages work consistently on every transport.
- Compression reduces payload size before the claim-check decision.
- Consumers do not need application-level DataBus code.

## Acceptance

- [ ] Gzip and Brotli are implemented behind network configuration.
- [ ] Final compressed bytes, not original bytes, determine DataBus offload.
- [ ] Claim-check is transport-neutral and proven over InMemory.
- [ ] Consumers retrieve, validate, decompress, and deserialize transparently.
- [ ] Provider lifecycle cleanup, not consumers, owns deletion.
- [ ] The [task board](../README.md) status for AZM-07 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
