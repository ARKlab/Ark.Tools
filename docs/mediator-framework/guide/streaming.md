# Streaming

Return `IAsyncEnumerable<T>` from a query handler when results should be
produced incrementally. Generated HTTP and gRPC endpoints preserve the stream
and stop enumerating when the caller disconnects or cancels.

## Stream from the handler

The sample uses the safest pattern: `ExecuteAsync` remains an `async` method,
then returns a separate iterator method.

```csharp
public sealed class WatchGreetingsHandler
    : IQueryHandler<WatchGreetingsQuery, IAsyncEnumerable<GreetingEvent>>
{
    public async Task<IAsyncEnumerable<GreetingEvent>> ExecuteAsync(
        WatchGreetingsQuery query,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return ReadEventsAsync(query, cancellationToken);
    }

    private async IAsyncEnumerable<GreetingEvent> ReadEventsAsync(
        WatchGreetingsQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in _events.ReadAsync(query.Id, cancellationToken))
            yield return item;
    }
}
```

Why this shape matters:

- the handler still follows the normal `async`/`await` guidance used across the repository;
- `[EnumeratorCancellation]` lets the generated transport cancel the iterator cleanly;
- the actual business enumeration stays in one method that can check cancellation inside the loop.

**Outcome:** HTTP JSON and gRPC clients start receiving items without waiting
for the complete sequence, and the cancellation token stops the upstream read.

## What HTTP callers receive

HTTP streaming stays plain JSON. It is not SSE framing.

Example request:

```http
GET /api/v1/greetings/stream?count=2&delayMilliseconds=1500
Authorization: ******
```

The sample test proves the first JSON object arrives before the producer has
finished the whole sequence. For a fast complete read, the body is a normal
JSON array such as:

```json
[
  { "index": 0, "message": "Hello, stream item 0!" },
  { "index": 1, "message": "Hello, stream item 1!" }
]
```

An empty stream is simply:

```json
[]
```

## What gRPC callers receive

gRPC consumers receive a normal server stream. The sample test consumes one
item, then cancels:

```csharp
using var call = client.GetGreetingsStream(
    new GetGreetingsStreamQuery { Count = 100, DelayMilliseconds = 1500 },
    new Metadata { { "authorization", "Bearer " + token } },
    cancellationToken: cancellation.Token);

(await call.ResponseStream.MoveNext(cancellation.Token).ConfigureAwait(false)).Should().BeTrue();
call.ResponseStream.Current.Index.Should().Be(0);
await cancellation.CancelAsync().ConfigureAwait(false);
```

Expected result after cancellation:

```text
RpcException with StatusCode = Cancelled
```

## Choose a suitable representation

| Consumer need | Use | Why |
| --- | --- | --- |
| Incremental browser/API consumer | HTTP JSON | Easy to call and inspect |
| Efficient typed streaming | gRPC | True stream semantics and typed client generation |
| Binary HTTP payload without unbounded buffering | Do not use MessagePack streaming | MessagePack needs the top-level length |
| Queue/worker processing | Rebus command + durable state | Rebus does not model streaming responses |

MessagePack responses are intentionally buffered into one array because the
format needs a top-level length. Set `MaxMessagePackStreamedItems` to a safe
ceiling when MessagePack is enabled; exceeding it returns a server error instead
of exhausting memory.

## Practical rules

- Validate obviously invalid inputs before returning the stream.
- Check cancellation inside the iterator loop, not only before it starts.
- Do not capture request-scoped mutable state that may disappear before the
  stream finishes.
- Test both a non-empty stream and an empty stream.

Use a custom transport adapter when the consumer requires SSE, bidirectional
streaming, or another framing protocol not provided by generated endpoints.

Architecture rationale: [design.md](../design.md).
