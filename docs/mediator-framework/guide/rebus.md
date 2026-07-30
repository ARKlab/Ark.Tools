# Rebus

`[RebusMessage]` makes a request or command available to generated Rebus
handlers. Delivery creates a message scope and then invokes the same
transport-neutral handler used by HTTP or gRPC.

## Declare ownership

```csharp
[RebusMessage(OwnerQueue = "greetings")]
public sealed record CompleteGreetingCommand : ICommand
{
    public required Guid Id { get; init; }
}
```

`OwnerQueue` names the queue responsible for handling the message. Leave it
unset only when the application's generated routing convention is sufficient.
Do not use a blank queue name.

**Outcome:** sending `CompleteGreetingCommand` routes it to `greetings`; the
receiver creates its scoped dependencies, calls
`ICommandHandler<CompleteGreetingCommand>`, and propagates delivery
cancellation to that handler.

## Generate type-based routing

The generator also emits `ConfigureArkRebusRouting<TAssemblyMarker>`. Call it
while configuring the Rebus transport so every `[RebusMessage(OwnerQueue = ...)]`
contract is mapped to the declared destination:

```csharp
Configure.With(activator)
    .Transport(t => t.UseInMemoryTransport(network, "sender"))
    .Routing(r => r.ConfigureArkRebusRouting<ApplicationAssemblyMarker>())
    .Start();
```

Generated routing is type based. The declaration below produces the equivalent
of `typeBased.Map<CompleteGreetingCommand>("greetings")`:

```csharp
[RebusMessage(OwnerQueue = "greetings")]
public sealed record CompleteGreetingCommand : ICommand;
```

| `OwnerQueue` value | Generated routing | Use case |
| --- | --- | --- |
| `"greetings"` | Maps this message type to `greetings`. | One owned worker queue. |
| `null`/omitted | No explicit type map. | A local message or an application-specific routing convention. |
| Empty/whitespace | Invalid. | Never valid; choose a queue or omit the property. |

Call `RegisterArkRebusHandlersFromAssembly<TAssemblyMarker>` in the receiving
application to register generated handlers, and call
`ConfigureArkRebusRouting<TAssemblyMarker>` in sending applications. A process
that both sends and receives normally calls both. Copy the generated routing
method rather than manually duplicating route maps; the source generator keeps
it synchronized with contract ownership.

## Compose synchronous and asynchronous work

Keep an immediate HTTP operation and delayed bus work as separate contracts:

```csharp
public async Task<GreetingResponse> ExecuteAsync(
    CreateGreetingRequest request,
    CancellationToken cancellationToken = default)
{
    var greeting = await _store.CreateAsync(request.Name, cancellationToken)
        .ConfigureAwait(false);
    await _bus.SendLocal(new CompleteGreetingCommand { Id = greeting.Id })
        .ConfigureAwait(false);
    return greeting;
}
```

The HTTP caller receives the created greeting. A worker later performs the
completion work under normal Rebus retry and error-queue behavior. Use an
outbox when persistence and sending must be atomic.

## Limits and escape hatch

Rebus messages are not streaming responses: an `IAsyncEnumerable<T>` result
cannot be meaningfully delivered and is rejected. Use a command plus durable
state for long-running work. Write `IHandleMessages<T>` directly for a legacy
message shape, custom retry policy, or bus behavior outside generated routing.

Architecture rationale: [design.md](../design.md).
