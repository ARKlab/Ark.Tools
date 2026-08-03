# Rebus

`[RebusMessage]` makes a request or command available to generated Rebus
handlers. Delivery creates a message scope and then invokes the same
transport-neutral handler used by HTTP or gRPC. Rebus is the best fit for
asynchronous work, retried delivery, and decoupled sender/worker processes.

## Attribute reference

| Member | Default | Meaning | Observable effect |
| --- | --- | --- | --- |
| `[RebusMessage]` | no explicit route | Generates Rebus wrapper(s) for the contract | The contract can be sent or received through generated Rebus glue |
| `OwnerQueue` | `null` | Queue that owns this message type | Generated routing maps the message type to that queue |

`OwnerQueue` may be omitted when a process-local or custom routing convention
already decides the destination. It must never be blank or whitespace.

## Declare ownership

```csharp
[RebusMessage(OwnerQueue = "greetings")]
public sealed record CompleteGreetingCommand : ICommand
{
    public required Guid Id { get; init; }
}
```

**Outcome:** sending `CompleteGreetingCommand` routes it to `greetings`; the
receiver creates its scoped dependencies, calls
`ICommandHandler<CompleteGreetingCommand>`, and propagates delivery
cancellation to that handler.

## Generate handlers and routing

A receiving process registers generated handlers. A sending process registers
generated routing. A process that does both usually calls both helpers.

```csharp
ArkGeneratedEndpoints.RegisterArkRebusHandlersFromAssembly<ApplicationAssemblyMarker>(container);

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
| `"greetings"` | Maps this message type to `greetings` | One owned worker queue |
| `null` / omitted | No explicit type map | A local message or application-specific routing convention |
| Empty / whitespace | Invalid | Never valid |

## Sample hosting pattern

`SampleComposition.BuildContainer(...)` is the reference setup. It adds:

- generated handler registration from the application assembly;
- `RebusScopeDecorator<>` so each message gets a SimpleInjector scope;
- optional protobuf serialization for Rebus messages;
- `AutomaticallyFlowUserContext(container)` so handlers see the caller identity;
- `ArkRetryStrategy(maxDeliveryAttempts: 1)` so failures dead-letter quickly.

That means the same validators, authorization decorators, and application
services used by HTTP and gRPC also run for Rebus messages.

## Compose synchronous and asynchronous work deliberately

Keep an immediate HTTP operation and delayed bus work as separate contracts when
they represent different public behaviors. The sample does this for greeting
composition:

```csharp
[HttpEndpoint("POST", "/api/v{version}/greetings/compose")]
public sealed record ComposeGreetingRequest : IRequest<ComposeGreetingResponse>;

[RebusMessage(OwnerQueue = "ark.mediator.sample")]
public sealed record CompleteGreetingCompositionRequest : IRequest<GreetingResponse>;
```

A handler or decorator can enqueue follow-up work:

```csharp
var greeting = await _store.CreateAsync(request.Name, cancellationToken)
    .ConfigureAwait(false);
await _bus.Send(new CompleteGreetingCommand { Id = greeting.Id })
    .ConfigureAwait(false);
return greeting;
```

Public outcome:

- the HTTP caller gets a normal immediate HTTP response;
- the worker later receives the queued message;
- retries and dead-letter behavior follow the Rebus host configuration.

## What to expect from routing

When `ConfigureArkRebusRouting<TAssemblyMarker>()` is in use, changing
`OwnerQueue` on a contract is a public operational change. The API-surface
snapshot records it and should be reviewed like any other queue-boundary change.

## Outbound-only hosts

Hosts that must not run a Rebus processor, such as Azure Functions, register only
the generated owner routing and configure a one-way transport:

```csharp
ApplicationComposition.RegisterOutboundRebus(
    container,
    transport => transport.UseAzureServiceBusAsOneWayClient(
        connectionString,
        new DefaultAzureCredential()),
    ArkGeneratedEndpoints.ConfigureArkRebusRouting<ApplicationAssemblyMarker>);
```

Do not register generated Rebus handlers, an input queue, subscriptions, workers,
or an outbox processor in an outbound-only host. Send owned messages with
`IBus.Send`; `SendLocal` requires a receiver in the current process.

Example snapshot line:

```text
REBUS CompleteGreetingCommand -> queue:greetings
```

## When not to use generated Rebus wiring

Rebus messages are not streaming responses: an `IAsyncEnumerable<T>` result
cannot be meaningfully delivered and is rejected. Write `IHandleMessages<T>`
directly when you need:

- a legacy message type you cannot annotate from the application assembly;
- custom retry or subscription behavior outside generated routing;
- transport-specific headers or topology that should not leak into shared contracts.

Architecture rationale: [design.md](../design.md).
