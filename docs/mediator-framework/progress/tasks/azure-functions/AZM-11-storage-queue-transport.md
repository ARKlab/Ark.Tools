# AZM-11 — Azure Storage Queue transport and trigger generation

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-05, AZM-08, AZM-09, AZM-10
**Scope**: TRANSPORT + GENERATOR
**Design**: [Transport abstraction](../../azure-functions-messaging-design.md#5-transport-abstraction-packaging-and-inmemory-transport), [Generated Functions surface](../../azure-functions-messaging-design.md#6-generated-functions-surface)

## Problem

Networks without `PubSub` should be able to run on the cheaper Azure Storage
Queue transport, including consuming. Storage Queue supports at-least-once
receive through the visibility timeout and `DequeueCount`, but has no
application properties, no topics, and no native dead-letter queue, so it
needs a text-safe envelope encoding, a poison-queue DLQ mapping, and its own
generated Azure Functions QueueTrigger.

## Execution map

- **Transport**: implement the Storage Queue transport
  (`Capabilities = Receive | ScheduledSend`; `Send` implicit; no `PubSub`) in
  `Ark.Tools.MediatorFramework.Messaging` using the AZM-05 contract.
- **Encoding**: the queue message body is a text-safe encoded envelope
  carrying the binary payload and the full `amf1-*` header set. The encoder
  must not assume the payload is JSON merely because the outer envelope is
  text-encoded.
- **Settlement mapping (QueueTrigger, not PeekLock)**: isolated QueueTrigger
  has no `MessageActions`. Complete = return successfully (host deletes).
  Abandon = throw (host applies `queues.visibilityTimeout` =
  network `RetryDelay`; default zero is invalid). Delivery count = native
  `DequeueCount` from bound `QueueMessage`. Immediate DLQ = `QueueClient`
  send to `<queue>-poison` with bounded metadata, `DeleteMessage` with the
  current pop receipt, then return successfully.
- **Poison ownership**: two actors can write `<queue>-poison` — the
  framework SDK move (metadata) and the Functions host after
  `queues.maxDequeueCount` failed throws (no metadata). Fail-fast,
  malformed envelopes, foreign `amf1-network`, and missing `IFailed<T>` at
  delivery `N` always use the SDK move. `maxDequeueCount` is `2N` when the
  network enables second-level retries, otherwise `N`. Verify that a
  successful return after SDK `DeleteMessage` is a completed invocation; if
  the host fails the invocation, record evidence and pick the first
  non-resurrecting alternative. The send-then-delete move is non-transactional;
  duplicate poison copies are acceptable and retain the original message ID.
- **Trigger generation**: extend
  `Ark.Tools.MediatorFramework.AzureFunctions.Generators` to emit a
  QueueTrigger for consumer hosts whose Functions host assembly declares
  `MessagingFunctionsTriggerBinding.StorageQueue`, reusing the AZM-10
  generation pipeline and the AZM-09 dispatcher. Verify the exact installed
  `Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues` API before
  emitting attributes.
- **Conformance**: run the send, scheduled-send, and receive/settlement groups
  of the AZM-05 transport conformance suite against Azurite (already used by
  repository tests). `PubSub` groups do not apply.
- **Runnable state**: at task end a consumer host can receive Book messages
  from Azurite through the transport pump, and the generated QueueTrigger
  compiles and dispatches; full solution builds and tests green.
- **Stop condition**: no topics, no subscriptions, no publish. `PubSub`
  members throw `NotSupportedException` naming the capability. Startup rejects
  this transport for networks declaring `PubSub`.

## Implementation steps

1. Implement the transport send path: encode the envelope (headers + binary
   payload) into a single text-safe body within Storage Queue size limits;
   account for encoding overhead when comparing against the network maximum
   payload threshold.
2. Implement scheduled send using the initial visibility delay, validating
   duration and due-time variants against transport and network limits.
3. Implement the Functions receive adapter: bind `QueueMessage`, pass
   `DequeueCount`/`MessageId`/`PopReceipt` into the AZM-09 dispatcher, and
   honor the Execution-map settlement table. Do not call `UpdateMessage` to
   emulate PeekLock abandon; throw instead. During function execution the
   host already extends visibility; do not fight that.
4. Implement the poison-queue DLQ: deterministic `<queue>-poison` name,
   created through the management seam. Immediate DLQ uses `QueueClient`
   send + delete + return as specified above.
5. Wire the retry policy: AZM-09 runs `IFailed<T>` at `DequeueCount == N`
   only when the network enables second-level retries. `host.json`
   `visibilityTimeout` equals `RetryDelay`. `maxDequeueCount` equals `2N` or
   `N` per the Execution map.
6. Declare `Capabilities = Receive | ScheduledSend`; verify AZM-01 startup
   validation rejects this transport for networks declaring `PubSub`, naming
   the capability.
7. Emit the generated QueueTrigger for `AzureStorageQueue`-bound consumer
   hosts: one trigger per identity queue, thin async methods passing the
   binding object and cancellation token to the settlement adapter in
   `Ark.Tools.MediatorFramework.AzureFunctions`, no per-contract logic.
8. Diagnose subscriptions or event usage on hosts whose network lacks
   `PubSub` (already covered by AZM-02 capability validation; add fixtures for
   the Storage Queue binding).
9. Reuse the shared DataBus claim-check unchanged: oversized compressed
   payloads offload before encoding.
10. Add configuration for connection/key names and managed identity following
    the repository Azure client conventions; no secrets in attributes.
11. Add XML documentation and API-surface entries for new public members and
    snapshot lines for generated Storage Queue triggers.
12. Add generator diagnostics for the `host.json` contract: when the
    consuming Functions host project supplies `host.json` through
    `AdditionalFiles`, parse `queues.maxDequeueCount` and
    `queues.visibilityTimeout` and warn (a new `ARKMF` warning) when either
    is missing or is not a valid literal. The generator must not execute the
    runtime retry-policy type. When `host.json` is not supplied, emit an
    information diagnostic recommending the `AdditionalFiles` opt-in.
13. Add a startup check in the Functions composition that reads the effective
    queues `MaxDequeueCount` and logs a structured NLog warning with the
    expected and actual values; an opt-in strict setting fails startup
    instead.

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with the
Storage Queue capability set, at-least-once visibility semantics, the
poison-queue DLQ mapping, the prominent `host.json` `maxDequeueCount`
poison-ownership contract, network-level second-level enablement, accepted
duplicate poison copies, text-safe encoding, scheduling limits, generated
QueueTriggers, and Azurite-based testing.

## Sample extension

Add a Book sample fixture composing a consumer host with the Storage Queue
transport against Azurite: send, scheduled send, receive, retry exhaustion,
and poison-queue dead-letter of a Book background message on a `Send`-only
network profile.

## Required test coverage

- Envelope encoding round-trips binary JSON, MessagePack, and protobuf
  payloads and all headers through a real Azurite queue.
- Scheduled send visibility behavior and limit validation.
- Receive, complete, abandon/visibility-expiry redelivery, and `DequeueCount`
  exactness.
- Dead-letter moves the envelope and failure metadata to `<queue>-poison` and
  removes the original; fault injection may produce duplicate poison copies,
  which retain the same original message ID.
- Retry exhaustion triggers the inline second-level flow at the configured
  delivery count.
- Startup rejects Storage Queue for networks requiring `PubSub`, naming the
  capability.
- `PubSub` members throw `NotSupportedException`.
- DataBus claim-check applies before encoding for oversized payloads.
- Generated QueueTrigger output is deterministic and byte-identical across
  runs.
- One portable identity/owner queue is used unchanged by Service Bus and
  Storage Queue trigger manifests.
- Conformance send/receive groups pass against Azurite.
- Fail-fast and malformed envelopes are SDK-moved to `<queue>-poison` with
  metadata, then the function returns successfully; the original is gone.
- Abandon is a thrown exception; the next visible time honors
  `RetryDelay`.
- The generator warns on missing or malformed `maxDequeueCount` or
  `visibilityTimeout` when `host.json` is supplied, and informs when it is
  not inspectable. Startup performs exact value comparison.
- A required extension-verification test records what the host does after
  SDK delete + successful return.
- Startup logs the expected-versus-actual `maxDequeueCount` warning; strict
  mode fails startup.
- Non-transactional poison move fault injection documents and accepts
  duplicate poison copies without losing the original message ID.

## Outcomes

- `Send`/`Receive` networks run end-to-end on the cheapest transport,
  including generated Functions consumers.
- Capability validation, not special-case diagnostics, enforces the no-PubSub
  shape.

## Acceptance

- [ ] Storage Queue transport implements the AZM-05 contract including receive
  settlement, `DequeueCount`, and the poison-queue DLQ.
- [ ] QueueTrigger complete=return, abandon=throw, immediate DLQ=SDK
  poison+delete+return, verified against the installed extension.
- [ ] Generator presence/shape diagnostics and startup exact validation cover
  the `host.json` `visibilityTimeout` and `N`/`2N` contract.
- [ ] Text-safe encoding preserves binary payloads and headers.
- [ ] Generated QueueTriggers dispatch through the AZM-09 runtime.
- [ ] Capability rejection and `NotSupportedException` behavior are tested.
- [ ] Conformance groups pass against Azurite.
- [ ] The [task board](../README.md) status for AZM-11 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
