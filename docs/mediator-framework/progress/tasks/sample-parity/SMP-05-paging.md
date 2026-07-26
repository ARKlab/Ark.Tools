# SMP-05 — Paging (G7)

**Category**: sample-parity · **Priority**: Release blocker · **Scope**: SAMPLE
**Depends on**: SMP-02 (SQL persistence; can be done against in-memory store if sequenced earlier).

## Problem

The mediator sample has no list/search endpoint with paging. The ReferenceProject exposes
Skip/Limit paged searches with a paged result envelope.

## Reference pattern

Search `samples/Ark.ReferenceProject` for the paged result type (e.g. `PagedResult`/`ListResult`
with `Count`/`Data`/`Skip`/`Limit` — check `Ark.Reference.Common` and API DTOs) and a search query
handler translating Skip/Limit to SQL.

## Steps

1. Add `SearchGreetingsQuery : IQuery<PagedResult<Greeting.V1.Output>>` with `Skip` (default 0),
   `Limit` (default e.g. 25, max-capped), optional filter/sort fields; `[HttpEndpoint("GET", ...)]`
   binding them from query string.
2. Reuse the common paged envelope if one exists in a referenced common package; otherwise declare a
   sample-local `PagedResult<T>` record. **Decision recorded**: a transport-agnostic paged envelope in
   the framework is out of scope for 1.0 — sample-level only; note it as a possible future framework
   addition in the PR.
3. Validate paging inputs via the SMP-01 FluentValidation validator (Limit bounds, Skip >= 0).
4. Handler implements SQL paging (OFFSET/FETCH) and total count.
5. gRPC: the same query surfaces automatically via the generator; verify the paged envelope
   round-trips protobuf (polymorphic/NodaTime members if any).
6. Tests: seeded data → page boundaries correct (`skip/limit` math, total count), invalid limit → 400.

## Outcomes

- Sample demonstrates paged search across HTTP and gRPC with validated inputs.

## Acceptance

- [ ] Paged search returns correct slices and total count (tests across at least 2 pages).
- [ ] Limit/Skip validation → 400 (test).
- [ ] Works over both HTTP and gRPC (parity test).
- [ ] Full solution build + tests green.
