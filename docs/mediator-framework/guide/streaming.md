# Streaming

Return `IAsyncEnumerable<T>` to stream results on HTTP and gRPC without
materializing the complete sequence. Honor cancellation at each iteration.
MessagePack responses are deliberately buffered and have a configured ceiling;
use JSON or gRPC when true incremental delivery is required.

```csharp
public sealed class GetGreetingsStreamHandler : IQueryHandler<GetGreetingsStreamQuery, IAsyncEnumerable<GreetingStreamItem>>
{
    public async Task<IAsyncEnumerable<GreetingStreamItem>> ExecuteAsync(GetGreetingsStreamQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await Task.CompletedTask.ConfigureAwait(false);
        return StreamAsync(query, ctk);
    }
}
```

Source: [`GreetingHandlers.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingHandlers.cs).

The iterator calls `ThrowIfCancellationRequested` and passes the token to
`Task.Delay`. A handwritten streaming adapter is the escape hatch for a wire
format with different framing. Rationale: [`design.md`](../design.md).
