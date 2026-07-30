# Rebus

`[RebusMessage]` turns a request or command into a generated Rebus handler.
`OwnerQueue` fixes ownership; otherwise generated routing follows the message
contract. Each delivery gets a per-message scope, and cancellation flows into
the handler.

```csharp
[RebusMessage(OwnerQueue = "ark.mediator.sample")]
public sealed record CompleteGreetingCompositionRequest : IRequest<GreetingResponse>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
```

Source: [`GreetingContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingContracts.cs).

Compose HTTP and bus work by giving the HTTP request only `[HttpEndpoint]` and
having its handler send the Rebus message:

```csharp
await _bus.SendLocal(new CompleteGreetingCompositionRequest { Id = id, Name = Request.Name }).ConfigureAwait(false);
```

Source: [`GreetingHandlers.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingHandlers.cs).

The escape hatch is a handwritten `IHandleMessages<T>` implementation when
legacy routing or bus behavior is required. Rationale:
[`design.md`](../design.md).
