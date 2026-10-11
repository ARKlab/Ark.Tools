# Contracts and handlers

A contract is the transport-neutral input to an application operation. A handler
owns the operation's behavior; endpoint generators only deserialize, authorize,
dispatch, and serialize.

Before adding or changing one, complete the [contract design
workflow](contract-design-workflow.md). It records transport exposure, ownership,
server-owned values, messaging participants, validation, authorization, versioning,
and test boundaries before implementation.

## Choose the contract shape

For model and operation naming, follow
[Request and DTO best practices](request-and-dto-best-practices.md). Keep
versioned `Input`/`Output` models separate from composed request envelopes.

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
Source: [`BookPrintProcessContracts.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.API/BookPrintProcessContracts.cs)

**Outcome:** the handler can be called by any enabled transport and remains
straightforward to test without an HTTP server, gRPC context, or message bus.

## Keep contracts stable

Use records for small immutable messages. Make every client-controlled member
explicit and keep server-owned values out of the contract. When a
contract crosses gRPC, add `[ProtoContract]` and a unique `[ProtoMember(n)]` to
every serialized member. The request, the response and each streamed item must be
protobuf contracts; a scalar such as `IAsyncEnumerable<int>` fails the build with
[ARKMF058](../analyzer-rules/ARKMF058.md):

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
Source: [`BookStreamingContracts.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.API/BookStreamingContracts.cs)

`ProtoMember` numbers are wire identifiers, not display order. Never reuse a
released number; add new optional members with new numbers. Replace or version a
contract instead of changing the meaning or type of an existing member.

## Keep server-owned values out of contracts

A contract carries only what the caller chooses. Values whose authoritative
source is the host, the authenticated principal, the tenant resolver, the clock,
the correlation context, or the handler are not contract members:

- read the user or tenant from the user context (`IContextProvider<ClaimsPrincipal>`)
  in the handler;
- take an identifier the client must not choose from the route;
- put server-generated values, such as an identifier or an audit timestamp, on
  the response type.

```csharp
[HttpEndpoint("POST", "/api/v{version}/orders")]
public sealed record CreateOrderRequest : IRequest<OrderResponse>
{
    public required string ProductCode { get; init; }

    public int Quantity { get; init; }
}

public sealed class CreateOrderHandler(IContextProvider<ClaimsPrincipal> user)
    : IRequestHandler<CreateOrderRequest, OrderResponse>
{
    public async Task<OrderResponse> ExecuteAsync(CreateOrderRequest request, CancellationToken ctk = default)
    {
        var userId = user.Current.Identity?.Name;
        // ...
    }
}
```
Source: [`BookPrintProcessFailureHandler.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.Application/Handlers/Book/BookPrintProcessFailureHandler.cs)

[ARKMF003](../analyzer-rules/ARKMF003.md) warns when an HTTP contract has a
property such as `TenantId` or `UserId` that looks server-owned.

## Handler boundaries

Handlers may depend on application services, repositories, clocks, and the
current-user abstraction. They must not depend on ASP.NET Core, gRPC server
types, Rebus message contexts, or client serialization. Put validation,
authorization, correlation, and transport conversion in decorators or host
configuration. Use a handwritten adapter only when a transport requirement
cannot be represented by an attribute; see [escape hatches](escape-hatches.md).

Architecture rationale: [design.md](../design/mediator-framework/design.md).
