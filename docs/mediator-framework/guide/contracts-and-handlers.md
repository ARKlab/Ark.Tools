# Contracts and handlers

A contract is the transport-neutral input to an application operation. A handler
owns the operation's behavior; endpoint generators only deserialize, authorize,
dispatch, and serialize.

## Choose the contract shape

| Need | Contract | Handler |
| --- | --- | --- |
| Read with a value | `IQuery<T>` | `IQueryHandler<TQuery, T>` |
| Operation with a value | `IRequest<T>` | `IRequestHandler<TRequest, T>` |
| Operation without a value | `ICommand` | `ICommandHandler<TCommand>` |

```csharp
public sealed record RenameGreetingCommand : ICommand
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}

public sealed class RenameGreetingHandler : ICommandHandler<RenameGreetingCommand>
{
    public async Task ExecuteAsync(
        RenameGreetingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await _repository.RenameAsync(command.Id, command.Name, cancellationToken)
            .ConfigureAwait(false);
    }
}
```

**Outcome:** the handler can be called by any enabled transport and remains
straightforward to test without an HTTP server, gRPC context, or message bus.

## Keep contracts stable

Use records for small immutable messages. Make every client-controlled member
explicit; use `[ServerSet]` rather than accepting server-owned values. When a
contract crosses gRPC, add `[ProtoContract]` and a unique `[ProtoMember(n)]` to
every serialized member:

```csharp
[ProtoContract]
public sealed record GreetingResponse
{
    [ProtoMember(1)]
    public required Guid Id { get; init; }

    [ProtoMember(2)]
    public required string Message { get; init; }
}
```

`ProtoMember` numbers are wire identifiers, not display order. Never reuse a
released number; add new optional members with new numbers. Replace or version a
contract instead of changing the meaning or type of an existing member.

## Handler boundaries

Handlers may depend on application services, repositories, clocks, and the
current-user abstraction. They must not depend on ASP.NET Core, gRPC server
types, Rebus message contexts, or client serialization. Put validation,
authorization, correlation, and transport conversion in decorators or host
configuration. Use a handwritten adapter only when a transport requirement
cannot be represented by an attribute; see [escape hatches](escape-hatches.md).

Architecture rationale: [design.md](../design.md).
