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
  (`Capabilities = Receive | ScheduledSend`; `Send` implicit; no `PubSub`;
  a 64 KiB final-message limit) in
  `Ark.Tools.MediatorFramework.Messaging` using the AZM-05 contract.
- **Encoding**: serialize the complete canonical binary envelope (binary
  payload plus the full `amf1-*` header set), Base64-encode it exactly once,
  and send the resulting text with `QueueMessageEncoding.None`. Generated
  Functions hosts set `extensions.queues.messageEncoding` to `none` and
  decode that raw Base64 body exactly once. The encoder must not assume the
  payload is JSON merely because the outer envelope is text-encoded.
- **Settlement mapping (QueueTrigger, not PeekLock)**: isolated QueueTrigger
  has no `MessageActions`. Complete = return successfully (host deletes).
  Abandon = throw (host applies `queues.visibilityTimeout` =
  participant `RetryDelay`; default zero is invalid). Delivery count = native
  `DequeueCount` from bound `QueueMessage`. Immediate DLQ = `QueueClient`
  send to `<queue>-poison` with bounded metadata, `DeleteMessage` with the
  current pop receipt, then return successfully.
- **Poison ownership**: two actors can write `<queue>-poison` — the
  framework SDK move (metadata) and the Functions host after
  `queues.maxDequeueCount` failed throws (no metadata). Fail-fast,
  malformed envelopes, foreign `amf1-network`, and missing `MessagingFailed<T>` at
  delivery `N` always use the SDK move. `maxDequeueCount` is `2N` when the
  participant enables second-level retries, otherwise `N`. A Functions app
  hosts exactly one messaging participant, so its host-wide queue settings
  have one unambiguous retry policy. Verify that a
  successful return after SDK `DeleteMessage` is a completed invocation; if
  the host fails the invocation, record evidence and pick the first
  non-resurrecting alternative. The send-then-delete move is non-transactional;
  duplicate poison copies are acceptable and retain the original message ID.
- **Trigger generation**: extend
  `Ark.Tools.MediatorFramework.AzureFunctions.Generators` to emit a
  QueueTrigger when the Functions host assembly binds a consumer participant
  through
  `[MessagingFunctionsHost(typeof(PrintingParticipant), MessagingFunctionsTriggerBinding.StorageQueue)]`,
  reusing the AZM-10
  generation pipeline and the AZM-09 dispatcher. Verify the exact installed
  `Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues` API before
  emitting attributes.
- **Conformance**: run the send, scheduled-send, and receive/settlement groups
  of the AZM-05 transport conformance suite against Azurite (already used by
  repository tests). `PubSub` groups do not apply.
- **Runnable state**: at task end a consumer participant can receive Book
  messages
  from Azurite through the transport pump, and the generated QueueTrigger
  compiles and dispatches; full solution builds and tests green.
- **Stop condition**: no topics, no subscriptions, no publish. `PubSub`
  members throw `NotSupportedException` naming the capability. Startup rejects
  this transport for networks declaring `PubSub`.

## Implementation steps

1. Implement the transport send path: serialize the complete canonical binary
   envelope (headers + binary payload), Base64-encode it once, and send it
   through an SDK client configured with `QueueMessageEncoding.None`. The
   transport measures the final text before send and before the AZM-07
   claim-check decision. Reserve 3 072 canonical bytes for bounded poison
   metadata: a normal inline envelope is at most 46 080 bytes and a poison
   envelope is at most 49 152 bytes, which Base64-encodes to at most 64 KiB.
   The bus offloads to DataBus before encoding when its complete candidate
   does not fit; it re-measures the attachment-reference envelope and fails
   explicitly if that cannot fit.
2. Implement scheduled send using the initial visibility delay, validating
   duration and due-time variants against transport and network limits.
3. Implement the Functions receive adapter: bind `QueueMessage`, pass
   `DequeueCount`/`MessageId`/`PopReceipt` into the AZM-09 dispatcher, and
   honor the Execution-map settlement table. Do not call `UpdateMessage` to
   emulate PeekLock abandon; throw instead. During function execution the
   host already extends visibility; do not fight that.
4. Implement queue provisioning and the poison-queue DLQ: startup ensures the
   participant identity queue and the deterministic `<queue>-poison` companion
   queue through the management seam when resource creation is enabled;
   both may be IaC-precreated, ensure is idempotent, and queues are never
   auto-deleted. Immediate DLQ uses a `QueueClient` configured with
   `QueueMessageEncoding.None` to send + delete + return as specified above.
5. Wire the retry policy: AZM-09 runs `MessagingFailed<T>` at `DequeueCount == N`
   only when the participant's retry policy enables second-level retries.
   `host.json`
   `visibilityTimeout` equals the participant's `RetryDelay`.
   `maxDequeueCount` equals `2N` or
   `N` per the Execution map.
6. Declare `Capabilities = Receive | ScheduledSend`; verify AZM-01 startup
   validation rejects this transport for networks declaring `PubSub`, naming
   the capability.
7. Emit the generated QueueTrigger for `StorageQueue`-bound consumer
   participants:
   one trigger per identity queue, thin async methods passing the
   binding object and cancellation token to the settlement adapter in
   `Ark.Tools.MediatorFramework.AzureFunctions`, no per-contract logic. Reuse
   the AZM-10 diagnostic that rejects more than one bound messaging
   participant in a Functions app.
8. Diagnose `Subscribes`/`Publishes` declarations on participants whose
   network lacks
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
    `AdditionalFiles`, parse `queues.messageEncoding`,
    `queues.maxDequeueCount`, and `queues.visibilityTimeout`. Warn (a new
    `ARKMF` warning) when `messageEncoding` is not literal `none`, or either
    retry setting is missing or malformed. The generator must not execute the
    runtime retry-policy type. When `host.json` is not supplied, emit an
    information diagnostic recommending the `AdditionalFiles` opt-in.
13. Add a startup check in the Functions composition that reads the effective
    queues `MaxDequeueCount` and logs a structured NLog warning with the
    expected and actual values; an opt-in strict setting fails startup
    instead.

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed. The single-Base64 wire contract, the
46 080/49 152 canonical caps, and the settlement table are fixed by the
design; the exact binary field layout below is conceptual.

Canonical envelope binary layout (before the single Base64 pass):

```text
canonical envelope (binary; layout conceptual, single-Base64 contract fixed)
+------------------------+--------------------------------------------------+
| varint headerCount     | number of header key/value pairs                 |
| repeated per header:   |                                                  |
|   varint keyLength     | followed by UTF-8 key bytes (amf1-*)             |
|   varint valueLength   | followed by UTF-8 value bytes                    |
| payload bytes          | remainder of the buffer, exactly as serialized   |
|                        | (or compressed / attachment-reference form)      |
+------------------------+--------------------------------------------------+
normal inline envelope  <= 46 080 canonical bytes (3 072 reserved for poison
                           metadata)
poison envelope         <= 49 152 canonical bytes -> Base64 <= 65 536 text
                           bytes (the 64 KiB queue-message ceiling)
```

Encoder/decoder skeleton — exactly one Base64 operation on each side, over a
transport-owned buffer, paired with `QueueMessageEncoding.None`:

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>Encodes and decodes the canonical Storage Queue envelope.</summary>
public static class StorageQueueEnvelopeCodec
{
    /// <summary>Writes headers and payload into the canonical layout, then Base64 text.</summary>
    public static string Encode(
        IReadOnlyDictionary<string, string> headers, in ReadOnlySequence<byte> payload)
    {
        // Transport-owned buffered representation; never exposed as byte[] to callers.
        var canonical = new ArrayBufferWriter<byte>();
        _writeVarInt(canonical, headers.Count);
        foreach (var (key, value) in headers)
        {
            _writeLengthPrefixedUtf8(canonical, key);
            _writeLengthPrefixedUtf8(canonical, value);
        }

        foreach (var segment in payload)
        {
            canonical.Write(segment.Span);
        }

        // Exactly one Base64 operation; the SDK client uses QueueMessageEncoding.None
        // and host.json requires queues.messageEncoding = "none".
        var encoded = new byte[Base64.GetMaxEncodedToUtf8Length(canonical.WrittenCount)];
        Base64.EncodeToUtf8(canonical.WrittenSpan, encoded, out _, out var written);
        return Encoding.UTF8.GetString(encoded.AsSpan(0, written));
    }

    /// <summary>Decodes the raw queue-message body back into headers and payload.</summary>
    public static (IReadOnlyDictionary<string, string> Headers, ReadOnlySequence<byte> Payload)
        Decode(BinaryData rawBody)
    {
        // Exactly one Base64 decode of the raw body; the remainder of the canonical
        // buffer is the binary payload as sent. The decoder must not assume the
        // payload is JSON merely because the outer representation is text.
        var text = rawBody.ToMemory();
        var canonical = new byte[Base64.GetMaxDecodedFromUtf8Length(text.Length)];
        if (Base64.DecodeFromUtf8(text.Span, canonical, out _, out var written)
            != OperationStatus.Done)
        {
            throw new MessagingFailFastException(MessagingFailFastReason.MalformedHeaders);
        }

        return _readCanonical(canonical.AsMemory(0, written));
    }
}
```

Size gate applied by the transport before send and before the AZM-07
claim-check decision, measuring the completed native representation:

```csharp
/// <summary>Fixed Storage Queue inline-envelope size caps.</summary>
public static class StorageQueueLimits
{
    /// <summary>Canonical bytes reserved for bounded poison metadata.</summary>
    public const int PoisonMetadataReservedBytes = 3_072;

    /// <summary>Maximum canonical bytes of a normal inline envelope.</summary>
    public const int MaximumNormalCanonicalBytes = 46_080;

    /// <summary>Maximum canonical bytes of a poison envelope (Base64 fits 64 KiB).</summary>
    public const int MaximumPoisonCanonicalBytes = 49_152;

    /// <summary>Final encoded queue-message text ceiling.</summary>
    public const int MaximumEncodedTextBytes = 65_536;
}

// On the transport (Capabilities = Receive | ScheduledSend; no PubSub):
public long? MaximumInlineEnvelopeBytes => StorageQueueLimits.MaximumEncodedTextBytes;

public long MeasureNative(
    IReadOnlyDictionary<string, string> headers, in ReadOnlySequence<byte> payload)
{
    // Measures the final encoded text of the complete candidate envelope. The bus
    // offloads to DataBus before encoding when the canonical size exceeds
    // MaximumNormalCanonicalBytes, re-measures the attachment-reference envelope,
    // and fails explicitly if that still cannot fit.
    var canonicalBytes = _measureCanonical(headers, payload);
    return Base64.GetMaxEncodedToUtf8Length(checked((int)canonicalBytes));
}
```

Generated QueueTrigger binding `QueueMessage` (Storage Queues Worker
extension 5.2.0+) with the three fixed settlement outcomes, plus the runtime
helper it delegates to:

```csharp
// <auto-generated />
#nullable enable
namespace Ark.MediatorFramework.AzureFunctions.Generated;

public static class ArkGeneratedFunctions
{
    /// <summary>Receives the "printing" participant identity queue.</summary>
    [global::Microsoft.Azure.Functions.Worker.Function("printing")]
    public static async global::System.Threading.Tasks.Task Printing(
        [global::Microsoft.Azure.Functions.Worker.QueueTrigger(
            "printing",
            Connection = "BookMessagingNetwork")]
        global::Azure.Storage.Queues.Models.QueueMessage message,
        global::System.Threading.CancellationToken cancellationToken)
    {
        // Complete = return; abandon = the helper rethrows so the host applies
        // queues.visibilityTimeout = RetryDelay; immediate DLQ = the helper SDK-moves
        // to "printing-poison", deletes by pop receipt, then returns successfully.
        await global::Ark.MediatorFramework.AzureFunctions.MessagingQueueFunctionsDispatcher
            .DispatchAsync(message, cancellationToken)
            .ConfigureAwait(false);
    }
}
```

```csharp
namespace Ark.MediatorFramework.AzureFunctions;

/// <summary>Maps QueueTrigger semantics onto the fixed settlement contract.</summary>
public sealed class MessagingQueueFunctionsDispatcher
{
    // Both clients are composed with QueueMessageEncoding.None (AZM-13).
    private readonly QueueClient _sourceQueue;   // "printing"
    private readonly QueueClient _poisonQueue;   // "printing-poison"

    /// <summary>Dispatches one delivery and applies the QueueTrigger settlement table.</summary>
    public async Task DispatchAsync(QueueMessage message, CancellationToken ctk)
    {
        try
        {
            var (headers, payload) = StorageQueueEnvelopeCodec.Decode(message.Body);
            // Header phase + generated binder + inline second-level per AZM-09; the
            // native delivery count is message.DequeueCount.
            await MessagingReceivePipeline.ProcessAsync(
                    new StorageQueueLockedDelivery(headers, payload, (int)message.DequeueCount),
                    ctk)
                .ConfigureAwait(false);
            // Complete: return successfully; the Functions host deletes the message.
        }
        catch (MessagingFailFastException failFast)
        {
            // Immediate DLQ: SDK move with bounded metadata (within the 3 072-byte
            // reservation), delete the original, then return success so the host does
            // not also retry. The move is non-transactional; duplicate poison copies
            // are accepted and keep the original message ID.
            var poisonBody = StorageQueueEnvelopeCodec.EncodePoison(message.Body, failFast.Reason);
            await _poisonQueue.SendMessageAsync(poisonBody, ctk).ConfigureAwait(false);
            await _sourceQueue.DeleteMessageAsync(message.MessageId, message.PopReceipt, ctk)
                .ConfigureAwait(false);
        }
        // Any other exception propagates (abandon): the host applies
        // visibilityTimeout = RetryDelay and, after maxDequeueCount, host-side poison.
    }
}
```

Required `host.json` fragment for a participant with `RetryDelay` = 30s and
`MaximumDeliveryCount` N = 5 with second-level retries enabled
(`maxDequeueCount` = 2N = 10; N when disabled):

```json
{
  "version": "2.0",
  "extensions": {
    "queues": {
      "messageEncoding": "none",
      "visibilityTimeout": "00:00:30",
      "maxDequeueCount": 10
    }
  }
}
```

Generator `host.json` diagnostic sketch over `AdditionalFiles`:

```csharp
// In the incremental generator: pair the StorageQueue host binding with host.json.
var hostJson = context.AdditionalTextsProvider
    .Where(static t => Path.GetFileName(t.Path)
        .Equals("host.json", StringComparison.OrdinalIgnoreCase))
    .Select(static (t, ct) => t.GetText(ct)?.ToString());

// When host.json is supplied:
//   - warn (new ARKMF diagnostic) when extensions.queues.messageEncoding is not the
//     literal "none";
//   - warn when queues.maxDequeueCount or queues.visibilityTimeout is missing or
//     malformed. The generator cannot execute the runtime retry-policy type, so the
//     exact N/2N and RetryDelay comparison is a startup check (expected-versus-actual
//     structured warning; opt-in strict mode fails startup).
// When host.json is not supplied: emit an information diagnostic recommending the
// AdditionalFiles opt-in.
```

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with the
Storage Queue capability set, at-least-once visibility semantics, the
poison-queue DLQ mapping, the prominent `host.json` `maxDequeueCount`
poison-ownership contract, participant-owned second-level enablement, accepted
duplicate poison copies, the canonical single-Base64 `messageEncoding: none`
wire format, the one-messaging-participant-per-Functions-app rule, the
prohibition on unrelated conflicting QueueTriggers, scheduling limits,
generated QueueTriggers, and Azurite-based testing.

## Sample extension

Add a Book sample fixture composing a consumer participant with the Storage
Queue
transport against Azurite: send, scheduled send, receive, retry exhaustion,
and poison-queue dead-letter of a Book background message on a `Send`-only
network declaration.

## Required test coverage

- Envelope encoding round-trips binary JSON, MessagePack, and protobuf
  payloads and all headers through a real Azurite queue with
  `messageEncoding: none`; the sender and trigger each perform exactly one
  Base64 operation.
- Inline and claim-check boundary tests prove that the final encoded normal
  and poison envelopes, including headers and bounded failure metadata, stay
  within 64 KiB.
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
  Storage Queue trigger manifests, and the AZM-02 50-character identity cap
  leaves room for the `-poison` companion queue.
- Conformance send/receive groups pass against Azurite.
- Startup ensures the participant identity queue and the `<queue>-poison`
  companion queue when resource creation is enabled, coexists with
  IaC-precreated queues, and never auto-deletes queues.
- Fail-fast and malformed envelopes are SDK-moved to `<queue>-poison` with
  metadata, then the function returns successfully; the original is gone.
- Abandon is a thrown exception; the next visible time honors
  `RetryDelay`.
- The generator warns on non-`none`, missing, or malformed
  `messageEncoding`, `maxDequeueCount`, or `visibilityTimeout` when
  `host.json` is supplied, and informs when it is not inspectable. Startup
  performs exact value comparison.
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
- One bound participant gives each Storage Queue Functions app one unambiguous
  host-wide retry and message-encoding configuration.

## Acceptance

- [ ] Storage Queue transport implements the AZM-05 contract including receive
  settlement, `DequeueCount`, and the poison-queue DLQ.
- [ ] QueueTrigger complete=return, abandon=throw, immediate DLQ=SDK
  poison+delete+return, verified against the installed extension.
- [ ] Generator presence/shape diagnostics and startup exact validation cover
  the `host.json` `messageEncoding`, `visibilityTimeout`, and `N`/`2N`
  contract.
- [ ] Text-safe encoding preserves binary payloads and headers.
- [ ] The single-Base64 `messageEncoding: none` contract and final encoded
  envelope-size boundaries are verified against Azurite.
- [ ] Generated QueueTriggers dispatch through the AZM-09 runtime.
- [ ] Capability rejection and `NotSupportedException` behavior are tested.
- [ ] Conformance groups pass against Azurite.
- [ ] The [task board](../README.md) status for AZM-11 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
