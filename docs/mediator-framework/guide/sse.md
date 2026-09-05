# Pushing updates with Server-Sent Events

Add `[Sse]` next to an existing `[HttpEndpoint("GET", …)]` to publish a sibling
`text/event-stream` route. The contract, route, versioning, authorization, and
OpenAPI metadata stay single-sourced on the HTTP endpoint.

Two shapes are supported, both with the same attribute:

| Contract result | Behavior |
| --- | --- |
| `Task<T>` (a normal query) | the endpoint re-executes the query every `IntervalSeconds` and emits an event when the result changes |
| `IAsyncEnumerable<T>` (a stream query) | the endpoint frames the handler's items as events |

## 1. Declare the endpoint

```csharp
[HttpEndpoint("GET", "/api/v{version}/books/{bookId}/reviews")]
[Sse(IntervalSeconds = 5, AllowClientInterval = true, MinimumIntervalSeconds = 2)]
public sealed record V1 : IQuery<V1, IReadOnlyList<BookReview>>
{
    [HttpRoute]
    public Guid BookId { get; init; }
}
```

This maps `GET /api/v1/books/{bookId}/reviews/sse` alongside the normal route.
`RouteSuffix` changes the suffix, `EventName` changes the event name (it defaults
to the generated contract name).

## 2. Consume it

```bash
curl -N -H "Accept: text/event-stream" \
  "https://host/api/v1/books/$id/reviews/sse?pollIntervalSeconds=10"
```

The browser's native `EventSource` cannot send an `Authorization` header: use
cookie authentication or a `fetch`-based reader. Never pass a token in the query
string — it lands in access logs. Serve SSE over HTTP/2, because browsers cap
six connections per origin on HTTP/1.1.

## What the runtime does per connection

- **Clamps the interval.** `pollIntervalSeconds` is honored only when
  `AllowClientInterval` is set, and is always clamped to
  `[MinimumIntervalSeconds, MaximumIntervalSeconds]`.
- **Re-runs every decorator per tick.** Validation and authorization are
  re-evaluated on each poll, exactly as for a normal request, because each tick
  goes through `IQueryProcessor`.
- **Emits only on change.** The `[ETag]` response property is the change token
  when the contract has one; otherwise the serialized payload is compared, which
  costs one serialization per tick. Set `EmitEveryTick = true` to emit
  unconditionally.
- **Sends the change token as `id:`**, so a client reconnecting with
  `Last-Event-ID` does not receive a duplicate first frame. There is no replay:
  a poller has no history, and missed frames are never re-sent.
- **Heartbeats when idle.** An idle connection emits an event named `heartbeat`
  every `HeartbeatSeconds`; clients must ignore it. Keep proxy and Kestrel idle
  timeouts above it.
- **Ends the connection** at `MaxConnectionSeconds` or at the bearer token's
  `exp`, whichever comes first, because the principal is captured once.
- **Skips, never queues.** A slow consumer misses ticks rather than accumulating
  them: SSE has no backpressure.

## Host limits

`AddArkMinimalApiHost` excludes `text/event-stream` from response compression.
Concurrent connections are capped per process and per principal; past the cap the
endpoint answers `503` with `Retry-After`. Override the defaults by registering a
singleton:

```csharp
services.AddSingleton(new ArkSseConnectionTracker(
    maxConcurrentConnections: 200,
    maxConcurrentConnectionsPerPrincipal: 2));
```

## When not to use it

The cost is `clients × 1/interval` query executions. Use SignalR instead when the
server must push on a real event rather than on a query result — at the price of a
backplane under a load balancer. An SSE connection is self-contained, so it needs
no backbone.

## Diagnostics

| Id | Meaning |
| --- | --- |
| `ARKMF021` | `[Sse]` without `[HttpEndpoint]` |
| `ARKMF022` | `[Sse]` on a contract that is not a query |
| `ARKMF023` | `[Sse]` on a non-`GET` endpoint |
| `ARKMF024` | invalid `[Sse]` configuration (interval bounds, heartbeat, route suffix) |

See the [SSE spike report](../sse-spike.md) for the design rationale and the full
security review.
