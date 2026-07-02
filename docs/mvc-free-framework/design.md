# Design: MVC-free, source-generated web services framework

## Goals

- Author business logic once as a **pure, transport-agnostic handler** and host
  it over Minimal API, code-first gRPC and Rebus simultaneously.
- Replace runtime reflection (MVC model binding, dynamic mediator dispatch) with
  **compile-time source generation**.
- Keep dependency resolution inside **SimpleInjector** (non-conforming,
  decorator-first), validated at startup.
- Keep C# as the single source of truth for contracts and routes.

## Non-goals

- Replacing Rebus with another bus.
- A proto-first (`.proto`-authored) workflow.
- Removing `Ark.Tools.Solid` contracts; the framework builds *on* them.
- AOT certification in the first iteration (design stays AOT-friendly).

## Layering

```
+-----------------------------------------------------------+
|  Pure handlers (Ark.Tools.Solid)                          |
|  IRequest<T> / IQuery<T> / ICommand + *Handler            |
|  - no HttpContext, no ServerCallContext, no MessageContext|
+-----------------------------------------------------------+
             ^ resolved from SimpleInjector async scope
+-----------------------------------------------------------+
|  Generated transport adapters (source generator output)   |
|  Minimal API endpoints | gRPC service impl | Rebus handler|
+-----------------------------------------------------------+
             ^ thin runtime support (this framework)
+-----------------------------------------------------------+
|  Transports: Kestrel HTTP/1.1+2 | gRPC HTTP/2 | Rebus bus |
+-----------------------------------------------------------+
```

## Contracts (C# as IDL)

A mutation/query is a pure record implementing the existing Solid contract and
decorated for protobuf so the same type serializes on gRPC and Rebus:

```csharp
[ProtoContract]
public sealed record CreateOrderRequest : IRequest<OrderResponse>
{
    [ProtoMember(1)] public Guid CustomerId { get; init; }
    [ProtoMember(2)] public IReadOnlyList<OrderItemDto> Items { get; init; } = [];
}
```

The handler is oblivious to transport:

```csharp
public sealed class CreateOrderHandler : IRequestHandler<CreateOrderRequest, OrderResponse>
{
    public Task<OrderResponse> ExecuteAsync(CreateOrderRequest request, CancellationToken ctk = default)
        => /* pure business logic */;
}
```

Routing/metadata is expressed with attributes on the request type (e.g.
`[ArkEndpoint("POST", "/api/v1/orders")]`, `[ServiceGroup("Orders")]`,
`[FromRoute]`, `[FromQuery]`). These attributes are the only transport hint the
developer writes; the generator turns them into concrete hosting code.

## The Roslyn incremental generator

An `IIncrementalGenerator` (superior to the legacy `ISourceGenerator`: cached,
re-runs only for changed syntax nodes) performs three phases:

1. **Syntax provider** — cheap predicate finds type declarations implementing
   `IRequest<>`/`IQuery<>`/`ICommand` and their handlers.
2. **Semantic analysis** — resolves the symbol, response type, routing
   attributes and parameter sources against the compilation's semantic model for
   type safety.
3. **Transport emission** — emits partial extension methods that wire the pure
   handler to each enabled transport.

### Emitted Minimal API

```csharp
app.MapPost("/api/v1/orders", async (
    CreateOrderRequest request, HttpContext ctx, CancellationToken ctk) =>
{
    var container = ctx.RequestServices.GetRequiredService<SimpleInjector.Container>();
    await using var scope = AsyncScopedLifestyle.BeginScope(container);
    // the caller identity flows through IContextProvider<ClaimsPrincipal> (HttpContext.User)
    var handler = container.GetInstance<IRequestHandler<CreateOrderRequest, OrderResponse>>();
    var result = await handler.ExecuteAsync(request, ctk).ConfigureAwait(false);
    return Results.Ok(result);
});
```

For route/query-bound queries the generator emits `[FromRoute]`/`[FromQuery]`
parameters and reconstructs the request object before dispatch.

### Emitted gRPC (code-first)

`protobuf-net.Grpc` hosts a `[ServiceContract]` interface. The generator groups
requests/queries (by namespace or `[ServiceGroup("Orders")]`) into one generated
`[ServiceContract]` interface plus an implementation that opens a SimpleInjector
scope and calls the same pure handler. Startup uses
`AddCodeFirstGrpc()` + `MapGrpcService<OrdersService>()`.

NodaTime members on contracts serialize over protobuf via the surrogates in
[`Ark.Tools.Nodatime.Protobuf`](../../src/common/Ark.Tools.Nodatime.Protobuf)
(`RuntimeTypeModel.AddNodaTimeSurrogates()`): `OffsetDateTime` preserves the
offset, `LocalDate` is date-only, `LocalDateTime` is zoneless and `Period` is
carried as its ISO-8601 round-trip string.

### Emitted Rebus handler

For request/command types marked as messages, the generator emits
`IHandleMessages<CreateOrderRequest>` whose `Handle` opens the SimpleInjector
scope (seeded from `MessageContext.Current.Headers`) and invokes the pure
handler. The message is the unit of work / transaction boundary via the Rebus
unit-of-work integration.

### Why resolve from SimpleInjector explicitly

Native Minimal API / gRPC parameter injection uses the conforming container.
To keep the domain graph in SimpleInjector (lifestyle scoping, decorators,
startup verification, no captive dependencies), generated adapters fetch the
`SimpleInjector.Container` from `RequestServices` and resolve the handler
themselves inside an `AsyncScopedLifestyle` scope.

## Error handling

| Transport | Mechanism | Mapping |
| --- | --- | --- |
| Minimal API | global exception handler / endpoint filter → `IProblemDetailsService` | `EntityNotFoundException`→404; `ValidationException`→400 + `extensions` field violations |
| gRPC | server interceptor → `Google.Rpc.Status` rich error model | field violations packed as `BadRequest` details in trailing metadata; thrown as `RpcException` |
| Rebus | unit-of-work rollback + native retry | exhausted → error/dead-letter queue with serialized exception headers |

Handlers only throw semantic domain exceptions; they never format transport
errors.

## User context

The caller identity is exposed to handlers through the existing
`IContextProvider<ClaimsPrincipal>` abstraction (`Ark.Tools.Solid`), reusing the
implementations already shipped in the repo — no new `IUserContext` type is
introduced:

- Minimal API: `AspNetCoreUserContextProvider` reads `HttpContext.User`
  (`ClaimsPrincipal`) via `IHttpContextAccessor`.
- gRPC: interceptor reads JWT/metadata from the HTTP/2 stream.
- Rebus: `RebusPrincipalContextProvider` reads the principal serialized in
  `MessageContext.Current.Headers`; the publisher injects it via `UserFlowStep`
  (`AutomaticallyFlowUserContext`), and
  `RebusPrincipalContextWithFallbackProvider` falls back when absent.

Handlers depend only on `IContextProvider<ClaimsPrincipal>` (e.g. via
`Ex.GetUserId()`); the hosting layer selects the transport-appropriate provider.

## Attachments and streaming

Pure requests reference the `IArkAttachment` abstraction (name, content type and
an `OpenRead()` stream) — a non-generic Attachment contract, not a generic
stream proxy:

- Minimal API: endpoint accepts `IFormFile`; the hosting maps
  `OpenReadStream()` into `ArkAttachment`.
- gRPC: emitted as `IAsyncEnumerable<>` streaming methods.

## Sample mapping to this design

The verifiable sample (`samples/Ark.MediatorFramework.Sample`) implements the
core of this design using dependencies already approved in the repo where
possible, and demonstrates the source generator on the Minimal API transport.
Mirroring the ReferenceProject, it separates the transport-agnostic
**Application** assembly (pure contracts/handlers, store, decorator) from the
**WebInterface** hosting assembly, where the selected requests/queries are
exposed via endpoints and the transports (user context, Rebus) are wired.
See [`implementation-plan.md`](implementation-plan.md) for exactly which pieces
are proven in code versus specified for follow-up, and [`tasks.md`](tasks.md)
for acceptance criteria.
