# Returning an items stream

Use `IAsyncEnumerable<T>` when the result is naturally produced over time or is
too large to buffer. The handler owns the iterator and must propagate the
caller cancellation token.

## 1. Define a stream query

```csharp
[HttpEndpoint("GET", "/api/v{version}/greetings/stream")]
[GrpcMethod("StreamGreetings")]
[GrpcService("Greetings")]
[ProtoContract]
public sealed record StreamGreetingsQuery :
    IQuery<StreamGreetingsQuery, IAsyncEnumerable<GreetingItem>>
{
    [HttpQuery]
    [ProtoMember(1)]
    public int Count { get; init; }
}

[ProtoContract]
public sealed record GreetingItem
{
    [ProtoMember(1)]
    public int Index { get; init; }

    [ProtoMember(2)]
    public required string Message { get; init; }
}
```
Source: [`BookStreamingContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.API/BookStreamingContracts.cs)

The same query is now eligible for generated HTTP JSON and gRPC server-stream
endpoints. It is not a Rebus response; a queue message has no caller stream.

## 2. Yield from the handler

Keep `ExecuteAsync` asynchronous, then return a separate iterator:

```csharp
public sealed class StreamGreetingsHandler :
    IQueryHandler<StreamGreetingsQuery, IAsyncEnumerable<GreetingItem>>
{
    public async Task<IAsyncEnumerable<GreetingItem>> ExecuteAsync(
        StreamGreetingsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Count < 0 || query.Count > 1000)
            throw new ArgumentOutOfRangeException(nameof(query.Count));

        await Task.CompletedTask.ConfigureAwait(false);
        return ReadAsync(query.Count, cancellationToken);
    }

    private static async IAsyncEnumerable<GreetingItem> ReadAsync(
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new GreetingItem
            {
                Index = index,
                Message = $"Hello, stream item {index}!",
            };
            await Task.Yield();
        }
    }
}
```
Source: [`StreamBooksHandler.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/Handlers/Book/StreamBooksHandler.cs)

The `[EnumeratorCancellation]` parameter allows generated transports to cancel
enumeration when a client disconnects. Check cancellation inside a long-running
loop or before each upstream read.

## 3. Map both generated transports

```csharp
services.AddCodeFirstGrpc();

[ArkGenerateMinimalApiForAssembly(typeof(StreamGreetingsQuery))]
public partial class StreamingEndpointContext
{
}

app.UseEndpoints(endpoints =>
{
    endpoints.MapArkEndpoints<StreamingEndpointContext>(
        versionPrefix: "/api/v{version}");
    endpoints.MapArkGrpcServicesFromAssembly<StreamGreetingsQuery>();
});
```
Source: [`SampleStartup.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/SampleStartup.cs)

## HTTP behavior

```http
GET /api/v1/greetings/stream?count=2
Authorization: Bearer <token>
```
Source: [`BookTransportBoundaryTests.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/BookTransportBoundaryTests.cs)

The generated JSON result is:

```json
[
  { "index": 0, "message": "Hello, stream item 0!" },
  { "index": 1, "message": "Hello, stream item 1!" }
]
```
Source: [`AsyncEnumerableStreamingTests.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/AsyncEnumerableStreamingTests.cs)

JSON clients observe items as the response is written. The exact buffering
behavior depends on the selected formatter; JSON is the default generated
representation. A zero-count stream returns `[]`.

## gRPC behavior

```csharp
using var call = client.StreamGreetings(
    new StreamGreetingsQuery { Count = 100 },
    cancellationToken: cancellation.Token);

(await call.ResponseStream.MoveNext(cancellation.Token)
    .ConfigureAwait(false)).Should().BeTrue();
call.ResponseStream.Current.Index.Should().Be(0);
await cancellation.CancelAsync().ConfigureAwait(false);
```
Source: [`BookTransportBoundaryTests.cs`](../../../samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/BookTransportBoundaryTests.cs)

The client receives a server stream and sees `Cancelled` when it cancels.

## Choose the right transport

| Need | Choice |
| --- | --- |
| Simple incremental API response | Generated HTTP JSON |
| Typed high-throughput stream | Generated gRPC |
| Browser SSE framing | `[Sse]` on the contract (see [SSE](sse.md)) |
| Bidirectional stream | Handwritten gRPC service |
| Background work | Rebus command plus durable progress |
| MessagePack stream | Avoid; top-level length requires buffering |

Set a safe count ceiling. Test a non-empty stream, an empty stream, invalid
counts, upstream cancellation, and a client disconnect.
