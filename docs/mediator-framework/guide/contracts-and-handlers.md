# Contracts and handlers

Use `IRequest<T>` for mutations with a result, `IQuery<T>` for reads, and
`ICommand` for operations without a result. A handler accepts only its contract,
application services, and `CancellationToken`; never inject HTTP, gRPC, or Rebus
types into it.

```csharp
public sealed record GetGreetingQuery : IQuery<GreetingResponse>
{
    [ProtoMember(1)]
    public Guid Id { get; init; }
}
public sealed class GetGreetingHandler : IQueryHandler<GetGreetingQuery, GreetingResponse>
{
    public async Task<GreetingResponse> ExecuteAsync(GetGreetingQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await _store.GetAsync(query.Id, ctk).ConfigureAwait(false);
    }
}
```

Source: [`GreetingContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingContracts.cs) and [`GreetingHandlers.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingHandlers.cs).

Records make immutable wire contracts convenient. Add `[ProtoContract]` and
`[ProtoMember(n)]` when a contract crosses protobuf; numbers are stable schema
identifiers, so never reuse a shipped number. Add new fields with new numbers.
`[MessagePackObject(true)]` enables the sample's keyed-by-name MessagePack
contract; use the project's configured resolver.

The escape hatch is a handwritten transport adapter; see
[escape hatches](escape-hatches.md). Architecture rationale:
[`design.md`](../design.md).
