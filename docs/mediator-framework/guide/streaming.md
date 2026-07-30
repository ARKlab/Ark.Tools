# Streaming

Return `IAsyncEnumerable<T>` from a query handler when results should be
produced incrementally. Generated HTTP and gRPC endpoints preserve the stream
and stop enumerating when the caller disconnects or cancels.

## Stream from the handler

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

**Outcome:** HTTP JSON and gRPC clients start receiving items without waiting
for the complete sequence, and the cancellation token stops the upstream read.

## Choose a suitable representation

Use gRPC or HTTP JSON for genuine incremental delivery. MessagePack responses
are intentionally buffered into one array because the format needs a top-level
length. Set `MaxMessagePackStreamedItems` to a safe ceiling when MessagePack is
enabled; exceeding it returns a server error instead of exhausting memory.

Rebus does not support streaming responses. Represent asynchronous work as a
message plus durable status instead. Use a custom transport adapter when the
consumer requires a framing protocol or bidirectional stream not provided by
generated endpoints.

Architecture rationale: [design.md](../design.md).
