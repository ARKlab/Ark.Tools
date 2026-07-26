# SMP-04 — Optimistic concurrency + ETag/If-Match (G6)

**Category**: sample-parity · **Priority**: Release blocker · **Scope**: SAMPLE (framework only if ETag emission is generator-level)
**Depends on**: SMP-02 (SQL persistence).

## Problem

The mediator sample has no concurrency story. The ReferenceProject registers an
`OptimisticConcurrencyRetrierDecorator`-style decorator and uses row versioning.

## Steps

1. Add a rowversion/version column to the Greeting table (SMP-02 schema).
2. Register the optimistic-concurrency retrier decorator around request/command handlers in
   `ApplicationComposition.cs`, mirroring the ReferenceProject registration in
   `samples/Ark.ReferenceProject/Core/Ark.Reference.Core.Application/Host/ApiHost.cs` (search
   `OptimisticConcurrency`). The decorator lives in framework packages — reuse, don't reimplement.
3. Update handlers/DAL to detect concurrent modification (`OptimisticConcurrencyException` from
   `Ark.Tools.Core`) — the shared ProblemDetails mapping (FW-03) turns it into **409 Conflict** when
   retries are exhausted.
4. **Optional ETag demo** (only if achievable without generator changes): expose the version as a
   response `ETag` and honor `If-Match` → 412 on mismatch. If this requires generator support for
   response-header emission, do **not** build it here — record a follow-up note in the PR and skip.
5. Tests: two concurrent updates → both succeed via retry (decorator) OR the loser gets 409 when
   business-conflicting; scenario for exhausted retries → 409.

## Outcomes

- Sample demonstrates the Ark optimistic-concurrency pattern end-to-end on the mediator stack.

## Acceptance

- [ ] Retrier decorator registered and effective (test provoking a concurrency conflict).
- [ ] Exhausted retries → 409 ProblemDetails (test).
- [ ] ETag/If-Match either demonstrated with a 412 test **or** explicitly deferred with a recorded follow-up.
- [ ] Full solution build + tests green.
