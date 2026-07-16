# NET-05 — Server-Sent Events transport spike (N5)

**Category**: aspnetcore · **Priority**: Post-release (spike) · **Scope**: FRAMEWORK (exploratory)

## Problem / opportunity

.NET 10 ships `TypedResults.ServerSentEvents`. A fourth mediator transport is conceivable:
`[SseEndpoint]` on a query whose handler returns `IAsyncEnumerable<T>`, generator-emitting an SSE
endpoint (typed events, reconnection ids).

## Spike scope (time-boxed; output is a report, optionally a prototype branch — no merge to main required)

1. Assess handler-shape fit: `IQueryHandler` returns `Task<T>`, not `IAsyncEnumerable<T>` — would SSE
   need a new handler kind (`IStreamQueryHandler<TQuery, TItem>`) in `Ark.Tools.Solid`? What are the
   decorator implications (validation/authorization decorators over streaming handlers)?
2. Prototype a hand-written SSE endpoint in the sample resolving a streaming handler; measure the
   generator delta needed.
3. Security review: authorization (SEC-01 semantics apply?), backpressure, connection limits.
4. Report findings + go/no-go recommendation into `docs/mediator-framework/` (extend
   `future-improvements.md` or a dedicated `sse-spike.md`).

## Outcomes

- A written go/no-go with the handler-kind design question answered and effort estimate for productization.

## Acceptance

- [ ] Spike report committed under `docs/mediator-framework/`.
- [ ] Prototype (if any) isolated (branch or `evaluations/`-style folder), not shipped in packages.
- [ ] Explicit recommendation and follow-up task list.
