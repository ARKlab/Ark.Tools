# AZM-04 — Multi-type envelope, content encoding, and serialization

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-01, AZM-02, AZM-03
**Scope**: RUNTIME + SERIALIZATION
**Design**: [Envelope and compatibility model](../../azure-functions-messaging-design.md#4-envelope-and-compatibility-model)

## Problem

One queue may contain multiple contract types and payload formats. The
envelope must carry enough metadata to select the contract and serializer
without relying on the host's current default, and must stay transport-neutral
so every transport adapter can map it to its native message shape.

## Execution map

- **Runtime project**: implement envelope/header/codec registries in
  `Ark.Tools.MediatorFramework.Messaging`; the envelope model is
  transport-neutral and references no Azure SDK type.
- **Existing integrations**: reuse Ark System.Text.Json, MessagePack, and
  protobuf abstractions already referenced by Mediator Framework projects; add
  no serializer package.
- **Contract lookup**: consume the generated registry from AZM-02 and expose no
  `Type.GetType` fallback. Generated metadata emits a frozen `typeof(T)` to
  current-name map for writes and a name-to-typed-deserializer dispatch table
  for reads, mirroring generated HTTP parameter binding and handler dispatch.
- **Testing**: place pure envelope/codec tests in
  `Ark.Tools.MediatorFramework.Tests`.
- **Runnable state**: the envelope and codecs are complete and fully tested in
  isolation; nothing sends or receives yet.
- **Stop condition**: do not send, receive, compress, or access DataBus in this
  task; define seams consumed by AZM-05/AZM-07/AZM-08/AZM-09. Transport-native
  mapping (Service Bus properties, Storage Queue text-safe encoding) belongs
  to the transport tasks AZM-10/AZM-11.

## Implementation steps

1. Define centralized native AMF type, message, correlation, sent-time,
   protocol, and failure header constants.
2. Define `amf1-*` header constants, including
   `amf1-content-type`, optional `amf1-content-encoding`,
   `amf1-payload-attachment-id`, `amf1-network` carrying the resolved producer
   network identity, and `amf1-sender-identity` carrying the participant that
   invoked `Send` or `Publish`.
3. Define a transport-neutral envelope abstraction with a binary payload and
   string metadata. Do not emit a delivery-count header; expose native
   delivery count only through runtime context.
4. Specify (but do not implement) the transport mapping requirement: each
   transport adapter maps the envelope to its native shape without losing
   binary payloads or headers. AZM-10/AZM-11 implement the mappings.
5. Implement a serializer registry for JSON, MessagePack, and protobuf using
   existing repository abstractions; do not add a third-party dependency
   without approval.
6. Use native content-type values: JSON
   `application/json;charset=utf-8`, protobuf `application/x-protobuf`, and
   MessagePack `application/x-msgpack`.
7. Resolve serializer and contract reads from the content-type and contract
   type headers. Preserve optional content-encoding and DataBus attachment
   metadata as opaque envelope headers for AZM-07; do not interpret them in
   this task. Receive must not depend on any participant default or retry
   settings;
   an unknown/unsupported protocol or type must produce a typed, fail-fast
   error. Senders always write the resolved network identity in
   `amf1-network` and `amf1-sender-identity` on both `Send` and `Publish`; a
   received `amf1-network` value that differs from the local
   network identity fails fast the same way, because it indicates a different
   network type sharing the receive entity, not a same-type wrong namespace.
8. Resolve writes from the contract owner's `DefaultSerializer` through the
   generated registry: the processing participant's default for messages, the
   publishing participant's default for events. Sender-side protocol choice
   does not exist; owner protocol and serializer-set incompatibilities are
   compile-time diagnostics (AZM-02).
9. Resolve contract types only through the generated registry; never perform
    unrestricted CLR type loading from `amf1-msg-type`.
10. Write the current logical contract name and resolve both current names and
    `FormerNames` aliases on receive.
11. Keep header/context construction separate from payload serde. Codecs write
    to `IBufferWriter<byte>` and read `ReadOnlySequence<byte>`; generated
    generic contract entries bind `T` to the serializer and the eventual
    processor call without runtime reflection.
12. Bound header count/size and serialized payload size before transport.
    Compressed/decompressed and attachment bounds belong to AZM-07.
13. Add deterministic round-trip and malformed-input diagnostics.

## Guide contribution

Update [`guide/serialization.md`](../../../guide/serialization.md) and the
Azure Functions guide with `amf1-*` headers, native content types,
header-driven serializer reads, and protocol retirement behavior. Compression
and claim-check guidance belongs to AZM-07.

## Sample extension

Extend the Book sample test fixtures so Book background message contracts can
be round-tripped through the envelope in every enabled protocol and multiple
types can share one logical queue. Pure in-process fixtures only; no transport
exists yet.

## Required test coverage

- Multiple message types in one logical queue.
- JSON, MessagePack, and protobuf round trips with binary payloads.
- Missing, unknown, uninstalled, and conflicting protocol headers.
- Unknown contract type and malformed payload.
- Correlation/message IDs and sent time use invariant formats.
- Sender identity round-trips for both send and publish; every participant has
  an identity (explicit or the normalized class-name default), so the sender
  identity is always the sending participant's identity.
- Optional content-encoding and DataBus attachment headers survive envelope
  round trips without being interpreted.
- Type-confusion attempts cannot resolve contracts outside the generated
  registry.
- Former-name aliases deserialize to the current contract; unknown names fail
  fast.
- A foreign `amf1-network` identity produces the typed fail-fast
  classification consumed by AZM-09; AZM-09 and the transport tasks verify
  physical dead-letter settlement.
- Oversized headers and serialized payloads fail fast before dispatch.

## Caveats

- Header-driven reads must not silently fall back to any participant default.
- Native AMF envelopes and Rebus messages are separate wire formats and are not
  interoperable.
- Do not log raw message bodies or sensitive failure details.

## Outcomes

- Any consumer can read every installed supported format selected by the
  message headers.
- Sending one protocol and reading another installed protocol is deterministic.
- Old messages are classified as fail-fast only when their codec is no longer
  installed or their contract is no longer registered; AZM-09 maps that
  classification to physical dead-letter settlement.

## Acceptance

- [x] All three protocols have registered implementations and tests.
- [x] A queue can contain multiple types and protocols without ambiguity.
- [x] The envelope model is transport-neutral and free of Azure SDK types.
- [x] Unsupported reads fail fast with bounded, serializable diagnostics.
- [x] No raw payload or secret metadata is logged.
- [x] The [task board](../README.md) status for AZM-04 is updated to this task's acceptance state.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
