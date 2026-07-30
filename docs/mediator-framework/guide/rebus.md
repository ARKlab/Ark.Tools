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
