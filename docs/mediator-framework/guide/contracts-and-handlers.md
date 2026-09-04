# Contracts and handlers

A contract is the transport-neutral input to an application operation. A handler
owns the operation's behavior; endpoint generators only deserialize, authorize,
dispatch, and serialize.

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
Source: [`BookPrintProcessContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.API/BookPrintProcessContracts.cs)

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
Source: [`BookStreamingContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.API/BookStreamingContracts.cs)

`ProtoMember` numbers are wire identifiers, not display order. Never reuse a
released number; add new optional members with new numbers. Replace or version a
contract instead of changing the meaning or type of an existing member.

## Protect server-owned values with `ServerSet`

Apply `[ServerSet]` to data whose authoritative value comes from the host,
authenticated principal, tenant resolver, clock, correlation context, or a
handler—not from the caller. The attribute is a binding and schema boundary:
the generated HTTP endpoint resets the property after input binding, generated
gRPC requests omit it, and generated OpenAPI request schemas omit it.

```csharp
[HttpEndpoint("POST", "/api/v{version}/orders")]
[GrpcMethod("CreateOrder")]
[GrpcService("Orders")]
public sealed record CreateOrderRequest : IRequest<OrderResponse>
{
    public required string ProductCode { get; init; }

    public int Quantity { get; init; }

    [ServerSet]
    public string? UserId { get; init; }

    [ServerSet]
    public string? TenantId { get; init; }
}
```
Source: [`BookContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.API/BookContracts.cs)

For this HTTP input, `ProductCode` and `Quantity` bind normally, but the
attempted `userId` and `tenantId` values do not reach the handler:

```json
{ "productCode": "SKU-42", "quantity": 2, "userId": "administrator" }
```
Source: [`BookContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.API/BookContracts.cs)

The host, decorator, or handler must then set trusted values before relying on
them. `ServerSet` prevents client binding; it does **not** populate the member,
authenticate a caller, or authorize an operation.

| Use `ServerSet` | Do not use `ServerSet` |
| --- | --- |
| Authenticated user/subject, tenant, correlation ID, server timestamp, generated audit values, or server-calculated defaults. | A client choice such as filter, page size, display name, requested delivery date, or optimistic concurrency token. |
| A field must never appear in generated gRPC input or HTTP request schemas. | The caller must provide it and validation must report a missing/invalid value. |
| You want one transport-neutral contract while preventing over-posting. | The value belongs only to a transport-specific adapter or endpoint. |

Do not mark a response-only property `ServerSet`; put it on the response type
instead. Do not use it as a substitute for authorization: a caller can still
invoke the operation unless the endpoint policy rejects them.

## Handler boundaries

Handlers may depend on application services, repositories, clocks, and the
current-user abstraction. They must not depend on ASP.NET Core, gRPC server
types, Rebus message contexts, or client serialization. Put validation,
authorization, correlation, and transport conversion in decorators or host
configuration. Use a handwritten adapter only when a transport requirement
cannot be represented by an attribute; see [escape hatches](escape-hatches.md).

Architecture rationale: [design.md](../design.md).
