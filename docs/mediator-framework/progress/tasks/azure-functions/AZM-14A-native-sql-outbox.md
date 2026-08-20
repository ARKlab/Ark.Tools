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
  validated headers and serialized body plus destination/scheduling metadata for both
  `Send` and `Publish`.
- **Processor hosting**: expose an opt-in `IHostedService` registration for a
  custom always-running process. It joins the configured network with the
  reserved hardcoded identity `outbox-processor`, owns no receive queue or
  subscriptions, and must be rejected by Azure Functions composition. The
  identity is reserved: AZM-02 rejects `[MessagingParticipant]` declarations
  using it, and startup validation rejects composition-supplied identities
  using it.
- **Dispatch seam**: drain persisted message headers and bodies through an internal
  transport sender. Do not reconstruct application contracts, rerun outgoing
  steps, or overwrite `amf1-sender-identity`.
- **Stop condition**: no polling loop starts in an Azure Functions process and
  no non-durable commit-then-send fallback exists.

## Implementation steps

1. Add transport-neutral enlistment for the framework `IBus` and
   `IOutboxContextCore` without adding outbox members to the public bus.
2. When enlisted, make every `Send` overload and `Publish` build and validate
   its final AMF headers and serialized body, including additional headers,
   `amf1-sender-identity`, destination, and scheduling metadata, then persist
   it through the existing outbox context in the application transaction.
3. Keep direct sending available when no outbox context is enlisted.
4. Add the native outbox processor as an `IHostedService` with bounded batch,
   cancellation, error backoff, and explicit structured diagnostics. Register
   it as the network participant `outbox-processor`.
5. Peek-lock a batch through `IOutboxContextCore`, send each persisted message
   through the configured transport, and commit deletion only after successful
   broker acceptance. A failed batch remains retryable.
6. Preserve the original sender identity and message ID during processor
   dispatch. The processor identity is operational metadata only and must not
   replace message headers or grant publish ownership after enqueue.
7. Validate public publish ownership, capability guards, reserved headers,
   scheduling bounds, serialization, compression, and DataBus claim-check
   before persistence. The processor sends the already validated headers/body and
   does not repeat application pipeline steps.
8. Provide separate composition extensions for native outbox enqueue and for
   hosting the processor. Functions composition may use only enqueue.
9. Fail startup when the processor is registered in an Azure Functions host,
   when more than one processor registration targets the same network/context,
   or when Rebus and native outbox adapters are mixed for one topology.
10. Add a dedicated always-running Book sample processor host beside the three
    messaging participants. Reuse the sample SQL and in-memory outbox profiles.

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

- `Send`, delayed `Send`, and `Publish` persist only after all validation and
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

- [ ] Native `Send` and `Publish` support transactional SQL outbox enqueue.
- [ ] The processor is an `IHostedService` with reserved identity
  `outbox-processor`; participant declarations and compositions using that
  identity are rejected.
- [ ] No outbox processor starts in Azure Functions composition.
- [ ] Original sender identity, headers, and body bytes survive durable dispatch.
- [ ] SQL locking, retry, cancellation, and failure behavior are tested.
- [ ] The [task board](../README.md) status for AZM-14A is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
