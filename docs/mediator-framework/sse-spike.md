# Server-Sent Events spike (NET-05)

**Outcome**: GO on the SSE poller over existing `IQuery` endpoints, GO on SSE framing for the existing
`IAsyncEnumerable` streaming branch, NO-GO on a new `IStreamQueryHandler` handler kind.

## Two shapes

| | A. SSE over a streaming query | B. SSE poller over an existing `IQuery` |
| --- | --- | --- |
| Handler shape | `IQueryHandler<Q, IAsyncEnumerable<T>>` — already supported | `IQueryHandler<Q, T>` returning `Task<T>` — no new handler kind |
| Generator delta | frame the items the streaming branch already produces | one emit branch reusing the existing binding/auth/OpenAPI path |
| Decorators | run once at subscribe: validation and authorization are evaluated for the whole connection lifetime | re-run per tick, exactly as a normal request does |
| New abstractions | none | none |

Both shapes are declared with the same `[Sse]` attribute, which supplements the `[HttpEndpoint("GET", …)]`
already on the contract. Nothing about routing, versioning, authorization or OpenAPI metadata is
re-declared, so there is a single source of truth per contract.

`IStreamQueryHandler` is unnecessary: shape B answers the question the original spike asked
(NET-05 §1) by sidestepping it. The entire feature is a loop that calls `IQueryProcessor.ExecuteAsync`
on a timer and wraps each result in `SseItem<T>`.

## Surface

```csharp
[HttpEndpoint("GET", "/api/v{version}/books/{bookId}/reviews")]
[Sse(IntervalSeconds = 60, AllowClientInterval = true)]
public sealed record V1 : IQuery<V1, IReadOnlyList<BookReview>> { … }
```

The generator emits a sibling route named after the behavior rather than the transport: `<template>/poller`
for a polled query and `<template>/stream` for a streaming one, both configurable with `RouteSuffix`. A sibling
route is preferred over `Accept: text/event-stream` negotiation on the original route: negotiation
would silently turn a normal `GET` into a never-ending response for any client that mis-sends the
header, and it would make the OpenAPI document lie about the operation's response.

Everything expensive lives in `Ark.Tools.MediatorFramework.MinimalApi` (`ArkSse`), so the generator
delta is one emit branch. `MinimalApi` is already `net10.0`-only, so `TypedResults.ServerSentEvents`
is available with no multi-targeting cost.

## Runtime behaviour

- **Interval**: declared server-side. A client may request an interval only when `AllowClientInterval`
  is set, and the value is always clamped to `[MinimumIntervalSeconds, MaximumIntervalSeconds]` —
  never trusted raw. The floor defaults to 60 seconds because cost scales with the client count.
  A declared interval below the floor is raised to the floor.
- **Change detection**: prefer the `[ETag]` response property when the contract has one; it is the
  framework's canonical change token. Without one, the serialized payload is compared byte-for-byte
  against the previous frame (this costs one serialization per tick). `EmitEveryTick = true` opts out.
- **Event id**: the change token is emitted as the SSE `id:`, so a reconnecting client that sends
  `Last-Event-ID` does not receive a duplicate first frame.
- **No replay, no backlog**: a poller has no history. `Last-Event-ID` only suppresses a duplicate; it
  never replays missed frames. If you need a backlog, you need a log, not a poller.
- **Heartbeat**: an idle connection emits an event named `heartbeat` (with no meaningful payload) so
  proxies and load balancers do not time the connection out. Clients must ignore it.
- **Backpressure**: SSE has none. `PeriodicTimer` keeps at most one pending tick, so a slow consumer
  skips polls instead of queueing them, and writes always observe `RequestAborted`.
- **Lifetime**: the connection is closed at `MaxConnectionSeconds` or at the bearer token's `exp`,
  whichever is sooner, because `HttpContext.User` is frozen when the connection opens.

`ponytail:` the cost model is `clients × 1/interval` query executions. The upgrade path is a per-instance
coalescing cache keyed by (contract, bound request, principal scope), but it must not be built first: it
is only correct when the query result is not principal-dependent, which the framework cannot infer.

## Security and operations

- **Authorization** is re-evaluated on every tick for shape B, because every tick goes through
  `IQueryProcessor` and therefore through the authorization decorator. SEC-01 semantics hold
  unchanged. Shape A evaluates once, at subscribe, like any other streaming response.
- **Browser auth**: the native `EventSource` API cannot set an `Authorization` header. Use cookie
  authentication or a `fetch`-based client. Do not pass tokens in the query string: they leak into
  access logs.
- **Connection caps**: `ArkSseConnectionTracker` caps concurrent connections per process and per
  principal, returning `503` with `Retry-After` past the cap. Register a configured instance as a
  singleton to override the defaults.
- **Compression**: `text/event-stream` is excluded from response compression by
  `AddArkMinimalApiHost`; compression buffers the response and would defeat frame flushing.
- **Proxies**: `X-Accel-Buffering: no` and `Cache-Control: no-cache, no-store` are set per connection,
  and response buffering is disabled. Reverse-proxy and Kestrel idle timeouts must exceed
  `HeartbeatSeconds`.
- **HTTP/2**: browsers cap 6 connections per origin on HTTP/1.1. Serve SSE over HTTP/2.

## SSE poller versus SignalR

Each SSE connection is stateless and self-contained: it holds nothing but the bound query, so it works
behind any load balancer with no backplane. That is precisely the property SignalR needs a backplane
(Redis, Azure SignalR) to achieve. Choose SignalR when the server must push on a real event and the
truth source is not a query. Choose the poller when the truth source *is* a query and you want no
backbone.

## Follow-ups

1. A coalescing cache for identical (contract, request, principal-scope) polls, if load demands it.
2. `Accept`-based negotiation, if a client ever needs the same route to serve both shapes.
3. A first-class client helper (the sample only documents `curl` and `fetch` usage).
