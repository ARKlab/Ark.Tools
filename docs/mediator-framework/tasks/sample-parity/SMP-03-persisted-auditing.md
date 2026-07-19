# SMP-03 — Persisted auditing (G5)

**Category**: sample-parity · **Priority**: Release blocker · **Scope**: SAMPLE
**Depends on**: SMP-02 (needs SQL persistence).

## Problem

The mediator sample's "auditing" is a counter-only decorator
(`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/CrossCutting.cs`).
The ReferenceProject persists an audit trail (`AuditContext` pattern) and exposes an audit query endpoint.

## Reference pattern (copy this)

Search `samples/Ark.ReferenceProject` for `Audit` (e.g. `AuditContext`, audit DAL types and the
audit controller/query in `Ark.Reference.Core.API`/`Application`): audit records are written in the
same data-context/transaction as the business change and are queryable via a paged endpoint.

## Steps

1. Add audit tables to the sample database project (SMP-02) mirroring the ReferenceProject schema.
2. Port the `AuditContext` integration into `SampleDataContext` so mutating handlers record audit
   entries (who = `IContextProvider<ClaimsPrincipal>`, what = contract name, when = `IClock`/NodaTime).
3. Replace or complement the counter decorator: keep the decorator as the write trigger if that's the
   ReferenceProject shape; otherwise write from the data context.
4. Add `GetAuditsQuery` (`[HttpEndpoint("GET", ...)]`) returning a paged audit list (coordinate with
   SMP-05 paging shape).
5. Tests (Reqnroll): create a greeting → audit query returns an entry with correct user/type; audit
   written in the same transaction (rollback test → no audit row).

## Outcomes

- Sample demonstrates persisted, queryable, transactional auditing.

## Acceptance

- [x] Mutations produce audit rows with user/type/timestamp (test).
- [x] Rolled-back mutation leaves no audit row (the audit insert uses the same SQL context transaction).
- [x] Audit query endpoint documented in OpenAPI and covered by a scenario.
- [x] Full solution build + tests green.
