# AZM-14A — Native SQL outbox and hosted processor

**Category**: azure-functions-messaging · **Priority**: reliability
**Depends on**: AZM-08, AZM-13, AZM-14
**Scope**: RUNTIME + OUTBOX + HOSTING + SAMPLE
**Design**: [Sample proof](../../azure-functions-messaging-design.md#12-sample-proof)

## Problem

Native Mediator Framework `Send` and `Publish` must participate in the same SQL
transaction as application state. Polling that durable outbox inside Azure
Functions would prevent clean scale-to-zero behavior, so enqueue and processing
must have separate composition paths.

## Execution map

- **Existing primitives**: reuse `Ark.Tools.Outbox`,
  `Ark.Tools.Outbox.SqlServer`, `IOutboxContextCore`, and existing SQL locking
  semantics. Do not add a parallel outbox schema or a new third-party package.
- **Producer integration**: add a native AMF outbox producer that persists the
  complete validated envelope plus destination/scheduling metadata for both
  `Send` and `Publish`.
- **Processor hosting**: expose an opt-in `IHostedService` registration for a
  custom always-running process. It joins the configured network with the
  reserved hardcoded identity `outbox-processor`, owns no receive queue or
  subscriptions, and must be rejected by Azure Functions composition. The
  identity is reserved: AZM-02 rejects `[MessagingParticipant]` declarations
  using it, and startup validation rejects composition-supplied identities
  using it.
- **Dispatch seam**: drain persisted raw envelopes through an internal
  transport sender. Do not reconstruct application contracts, rerun outgoing
  steps, or overwrite `amf1-sender-identity`.
- **Stop condition**: no polling loop starts in an Azure Functions process and
  no non-durable commit-then-send fallback exists.

## Implementation steps

1. Add transport-neutral enlistment for the framework `IBus` and
   `IOutboxContextCore` without adding outbox members to the public bus.
2. When enlisted, make every `Send` overload and `Publish` build and validate
   its final AMF envelope, including additional headers,
   `amf1-sender-identity`, destination, and scheduling metadata, then persist
   it through the existing outbox context in the application transaction.
3. Keep direct sending available when no outbox context is enlisted.
4. Add the native outbox processor as an `IHostedService` with bounded batch,
   cancellation, error backoff, and explicit structured diagnostics. Register
   it as the network participant `outbox-processor`.
5. Peek-lock a batch through `IOutboxContextCore`, send each persisted envelope
   through the configured transport, and commit deletion only after successful
   broker acceptance. A failed batch remains retryable.
6. Preserve the original sender identity and message ID during processor
   dispatch. The processor identity is operational metadata only and must not
   replace envelope headers or grant publish ownership after enqueue.
7. Validate public publish ownership, capability guards, reserved headers,
   scheduling bounds, serialization, compression, and DataBus claim-check
   before persistence. The processor sends the already validated envelope and
   does not repeat application pipeline steps.
8. Provide separate composition extensions for native outbox enqueue and for
   hosting the processor. Functions composition may use only enqueue.
9. Fail startup when the processor is registered in an Azure Functions host,
   when more than one processor registration targets the same network/context,
   or when Rebus and native outbox adapters are mixed for one topology.
10. Add a dedicated always-running Book sample processor host beside the three
    messaging participants. Reuse the sample SQL and in-memory outbox profiles.

## Core code shapes

Conceptual shapes — final public names are selected by this task; the
signatures' invariants are fixed. Outbox names below are the real
`Ark.Tools.Outbox` abstractions: `IOutboxContextCore.SendAsync` /
`PeekLockMessagesAsync`, `OutboxMessage { Headers, Body }`,
`IOutboxAsyncContextFactory`, and the `OutboxProcessorBase` polling loop.

Enqueue path — when an `IOutboxContextCore` is enlisted, the fully validated
envelope (headers + payload + destination/scheduling metadata) is persisted
within the ambient SQL transaction instead of being sent directly:

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>Bus decoration that persists validated envelopes through an enlisted outbox.</summary>
public sealed class OutboxEnlistedBus : IBus
{
    private readonly IOutboxContextCore _outbox;

    /// <inheritdoc/>
    public async Task Send<T>(
        T message,
        Dictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default) where T : class
    {
        // Full validation and the outgoing pipeline run BEFORE persistence: publish
        // ownership, capability guards, reserved-header rejection, serialization,
        // compression, DataBus claim-check, and native measurement. The persisted
        // envelope already carries amf1-sender-identity (the enqueuing participant).
        var (headers, payload, destinationQueue) =
            await _prepareValidatedEnvelopeAsync(message, additionalHeaders, cancellationToken)
                .ConfigureAwait(false);

        // Destination and scheduling ride in framework-reserved outbox headers so the
        // existing Headers + Body schema is reused unchanged (header names conceptual).
        headers["amf1-outbox-destination-kind"] = "queue"; // "topic" for Publish
        headers["amf1-outbox-destination"] = destinationQueue;

        // OutboxMessage.Body is the existing persistence contract of Ark.Tools.Outbox,
        // not a framework-facing payload API; the transport-owned buffer is copied once.
        await _outbox.SendAsync(
                new[] { new OutboxMessage { Headers = headers, Body = payload.ToArray() } },
                cancellationToken)
            .ConfigureAwait(false);
        // Commits or rolls back atomically with the application state of the same
        // SQL context. Without an enlisted outbox context, Send/Publish go directly
        // to the transport.
    }

    // Send(T, TimeSpan, ...), Send(T, DateTimeOffset, ...) additionally persist
    // "amf1-outbox-due-time"; Publish<T> persists the derived topic destination.
}
```

Processor — an `IHostedService` built on the real `OutboxProcessorBase`
polling loop, registered under the reserved identity, peek-locking batches
and committing deletion only after the transport accepts every send:

```csharp
namespace Ark.MediatorFramework.Messaging;

/// <summary>Drains the native outbox through the configured network transport.</summary>
public sealed class MessagingOutboxProcessor : OutboxProcessorBase, IHostedService
{
    /// <summary>Reserved network identity of the running processor.</summary>
    public const string Identity = "outbox-processor";

    private readonly IOutboxAsyncContextFactory _contextFactory;
    private readonly IMessagingTransport _transport;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    protected override async ValueTask<IOutboxContextCore> CreateContextAsync(
        CancellationToken ctk)
    {
        return await _contextFactory.CreateAsync(ctk).ConfigureAwait(false);
    }

    protected override async Task ProcessMessagesAsync(
        IReadOnlyList<OutboxMessage> messages, CancellationToken ctk)
    {
        foreach (var message in messages)
        {
            // Raw-envelope dispatch seam: strip the reserved outbox headers, send the
            // persisted headers/payload as-is. Never reconstruct application contracts,
            // rerun outgoing steps, or overwrite amf1-sender-identity.
            var (headers, destination, dueTime) = _splitReservedOutboxHeaders(message.Headers!);
            var payload = new ReadOnlySequence<byte>(message.Body!);

            await _transport.SendAsync(destination, headers, payload, dueTime, ctk)
                .ConfigureAwait(false);
        }
        // The base loop commits the peek-locked batch (deleting the rows) only after
        // every send succeeded; a failed batch stays locked-then-retryable with the
        // base class error backoff.
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Startup composition rejects this registration inside an Azure Functions host
        // and rejects any participant declared or composed under the reserved identity.
        _cts = new CancellationTokenSource();
        _loop = ProcessLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_loop is not null)
        {
            await _loop.ConfigureAwait(false);
        }
    }
}
```

Persisted row shape — the existing `Ark.Tools.Outbox.SqlServer` table is
reused unchanged; destination metadata lives in the headers JSON:

```text
[ops].[Outbox]  (existing Ark.Tools.Outbox.SqlServer schema; no new columns)
  [Id]      bigint IDENTITY(1,1) NOT NULL   -- peek-lock/delete key, insertion order
  [Headers] nvarchar(MAX)        NOT NULL   -- JSON header map: full amf1-* envelope
                                            -- headers (msg-type, msg-id, corr-id,
                                            -- senttime, network, sender-identity,
                                            -- content-type/encoding, attachment refs)
                                            -- plus reserved outbox routing headers:
                                            --   amf1-outbox-destination-kind
                                            --   amf1-outbox-destination
                                            --   amf1-outbox-due-time (optional)
  [Body]    varbinary(MAX)       NOT NULL   -- validated payload bytes exactly as
                                            -- serialized/compressed at enqueue time
```

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md),
[`guide/host-setup-and-composition.md`](../../../guide/host-setup-and-composition.md),
and [`guide/rebus.md`](../../../guide/rebus.md) with transactional enqueue,
the separate processor topology, the reserved identity, direct-send behavior,
and Rebus/native outbox selection.

## Sample extension

In native Mediator Framework mode, configure the Book application data context
with the existing SQL outbox and add a separate custom host that runs the
network outbox `IHostedService`. The Functions subscribers may enqueue outgoing
messages/events but never host the processor. Keep the existing Rebus
WebInterface and RebusProcessor outbox registrations unchanged.

## Required test coverage

- `Send`, `Defer`, and `Publish` persist only after all validation and
  preserve optional additional headers.
- Application state and outbox records commit atomically in SQL.
- Rollback persists neither application state nor outbox records.
- The processor preserves message ID, network, original sender identity,
  destination, schedule, content, compression, and DataBus headers.
- Successful dispatch deletes/commits the locked outbox batch.
- Failed dispatch leaves messages retryable and applies bounded backoff without
  reporting success.
- Concurrent processor attempts do not double-lock one row; duplicate broker
  delivery remains covered by normal at-least-once semantics.
- Functions composition cannot resolve or start the processor.
- A participant declaration using the reserved `outbox-processor` identity is
  rejected at compile time, and startup rejects registering a participant
  under it.
- The custom host resolves one `IHostedService` under identity
  `outbox-processor` and shuts down cooperatively.
- Rebus and native outbox adapters remain mutually exclusive per topology.

## Outcomes

- Native Mediator Framework sends and publishes have durable SQL outbox
  support.
- Azure Functions remains scale-to-zero friendly because it only enqueues.
- A framework-supported custom host reliably drains the network outbox.

## Acceptance

- [x] Native `Send` and `Publish` support transactional SQL outbox enqueue.
- [x] The processor is an `IHostedService` with reserved identity
  `outbox-processor`; participant declarations and compositions using that
  identity are rejected.
- [x] No outbox processor starts in Azure Functions composition.
- [x] Original sender identity and envelope bytes survive durable dispatch.
- [x] SQL locking, retry, cancellation, and failure behavior are tested.
- [x] The [task board](../README.md) status for AZM-14A is updated to this task's acceptance state.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
