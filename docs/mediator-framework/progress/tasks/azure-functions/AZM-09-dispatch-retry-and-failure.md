# AZM-09 — Scoped dispatch, settlement, retries, and second-level failure

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-04, AZM-05, AZM-06, AZM-08
**Scope**: RUNTIME
**Design**: [Dispatch and scope semantics](../../azure-functions-messaging-design.md#7-dispatch-and-scope-semantics)

## Problem

Receive processing must reproduce the important Rebus processing semantics
without a Rebus processor: fresh scopes, fail-fast DLQ, delivery-count
exhaustion, and inline second-level dispatch. The dispatcher targets the
AZM-05 transport receive contract, so the identical code runs under the
InMemory pump now and under generated Service Bus triggers in AZM-10.

## Execution map

- **Public API**: define framework `IFailed<T>`, exception DTO, and incoming
  message context in `Ark.Tools.MediatorFramework`.
- **Runtime**: implement manual settlement and scoped dispatch in
  `Ark.Tools.MediatorFramework.Messaging` against the transport receive
  contract (locked delivery + native delivery count + complete/abandon/
  dead-letter). No Azure SDK type appears in the dispatcher.
- **Exact exhaustion rule**: second-level retries are enabled or disabled by
  the participant's retry policy, never inferred from handler registrations.
  When disabled,
  deliveries `1..N` run normal `T` and max delivery is `N`. When enabled,
  deliveries `1..N-1` run normal `T` (fail-fast → immediate DLQ, otherwise
  abandon). Delivery `N` runs inline `IFailed<T>` in a fresh scope, or
  immediate DLQ if no handler is registered. Missing `IFailed<T>` is a
  fail-fast condition. `IFailed` success completes; fail-fast throw
  dead-letters; any other throw abandons. Deliveries `N+1..2N` run normal `T`
  again until the transport max (`2N`) dead-letters. `IFailed` runs once, at
  `N`.
- **Lock discipline**: automatic completion is forbidden; configure bounded
  automatic lock renewal; treat lock loss/completion failure as unsuccessful
  processing.
- **Runnable state**: at task end, Book messages sent via AZM-08 are received,
  dispatched, retried, and failure-handled over the InMemory transport; full
  solution builds and tests green.
- **Stop condition**: never send or persist `IFailed<T>` and never perform
  second-level dispatch for malformed/unsupported envelopes or fail-fast
  exceptions.

## Implementation steps

1. Implement typed envelope-to-contract dispatch with one
   `AsyncScopedLifestyle` scope for normal handling, plugged into the AZM-05
   runtime message pump. The generated name-to-deserializer dispatch table
   must deserialize with a closed generic `T` and call the corresponding
   generic processor method, like generated Minimal API and HttpTrigger
   parameter binding/handler dispatch; reflection is forbidden.
2. Populate message context and cancellation before handler resolution.
3. Complete/ack only after successful handler completion.
4. Translate the existing fail-fast marker/mechanism into direct dead-letter
   settlement on the receiving transport.
5. Use the native delivery count from the locked delivery and the
   participant's retry policy (declared or framework default, AZM-02) for
   retry exhaustion. Never copy or increment it in message
   headers. Configure InMemory max delivery to `2N` when second-level retries
   are enabled and `N` otherwise.
6. Define the public transport-neutral `IFailed<T>` containing the original
   message, serializable exception info, and a read-only native
   delivery-count snapshot. Do not require failure headers on the live
   envelope; attach bounded details only when dead-lettering if the
   transport can carry them.
7. Dispatch the failure wrapper inline in a fresh SimpleInjector scope from the
   catch path at delivery `N` only. Do not enqueue a separate second-level
   message; no `IFailed<T>` is persisted on the bus.
8. If no `IFailed<T>` handler is registered at delivery `N`, dead-letter
   immediately as fail-fast. If the handler throws fail-fast, dead-letter.
   Otherwise abandon and allow normal `T` on later deliveries through `2N`.
9. Make malformed/unsupported envelopes fail fast and prevent second-level
   dispatch.
10. Apply the same settlement policy to an exception propagated by an AZM-06
    pipeline step as to a handler exception: fail-fast dead-letters and every
    other exception abandons. AZM-06 tests propagation only; this task owns
    the physical settlement assertion.
11. Add structured NLog logging with invariant formatting and bounded metadata.
12. Validate manual settlement, lock renewal, and lock-loss behavior through
    the transport contract.
13. Add decision-lock tests for the selected retry strategy and document the
    alternatives from the design: Ark/Rebus terminal second-level failure,
    explicit deferred second-level handling, and delayed first-level
    rescheduling.

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with
scope, settlement, the `N`/`2N` delivery table, fail-fast DLQ, and inline
`IFailed<T>` at delivery `N`. Document that abandon delay is transport-
specific: InMemory/`RetryDelay` and Storage Queue `visibilityTimeout` wait;
Service Bus abandon is immediate.

## Sample extension

Run the Book printing completion, failure, retry-exhaustion, and second-level
scenarios end-to-end over the InMemory transport using the framework bus and
dispatcher. The existing Rebus processor path remains untouched and green.

## Required test coverage

- Successful typed dispatch and completion.
- Fresh scope and cancellation propagation.
- Handler failure followed by retry and eventual success.
- Pipeline-step failure follows the same tested settlement policy as a handler
  failure.
- Delivery `N` with no `IFailed<T>` handler dead-letters immediately.
- Second-level disabled runs normal `T` through `N` and never resolves
  `IFailed<T>`.
- Participant retry policy validation rejects `N = 1` when second-level
  retries are enabled.
- Fail-fast exception goes directly to DLQ at any delivery.
- Inline second-level handler receives original message and serializable error
  info at delivery `N` only.
- Second-level dispatch is inline and uses a separate SimpleInjector scope;
  no failure message is persisted.
- `IFailed` fail-fast throw dead-letters; other throw abandons and the next
  delivery is normal `T`, not `IFailed` again.
- InMemory abandon waits `RetryDelay` on the test clock.
- Lock loss or failed completion is surfaced and permits duplicate delivery.
- Unsupported protocol/type never enters second-level dispatch.
- Native delivery count is unchanged in the message and available in runtime
  context/failure diagnostics only.
- Duplicate delivery remains safe under at-least-once semantics.

## Outcomes

- Receive processing has explicit, tested settlement behavior on a real
  (InMemory) transport before any Azure binding exists.
- Second-level handling uses Rebus concepts without claiming identical retry
  behavior or a Rebus dependency.
- Every failure path is observable and bounded.

## Acceptance

- [ ] Normal and second-level handling use separate SimpleInjector scopes.
- [ ] Fail-fast and unsupported-read paths go directly to DLQ.
- [ ] With second-level enabled, delivery `N` runs `IFailed<T>` or immediate
  DLQ and max delivery is `2N`.
- [ ] Participant retry policy, not handler discovery, selects `N` or
  `2N` behavior.
- [ ] Native delivery count controls retry exhaustion.
- [ ] Failure metadata is serializable, bounded, and tested.
- [ ] Structured logging contains no interpolated messages or raw bodies.
- [ ] Book scenarios run end-to-end over InMemory.
- [ ] The [task board](../README.md) status for AZM-09 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
