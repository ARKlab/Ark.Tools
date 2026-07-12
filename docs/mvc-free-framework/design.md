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

Routing/metadata is expressed with **explicit, per-transport** attributes on the
request type. Each transport is opt-in and declared independently:

- `[HttpEndpoint("POST", "/api/v{version}/orders")]` — expose over Minimal API for each active version.
- `[GrpcMethod]` (optionally `[GrpcMethod("CreateOrder")]`) — expose as a
  code-first gRPC method; `[ServiceGroup("Orders")]` groups the service.
- `[RebusMessage]` — expose as a Rebus message.

`[FromRoute]`/`[FromQuery]` refine the HTTP binding. These attributes are the
only transport hint the developer writes; the generator turns each *declared*
transport into concrete hosting code. A request/query with **no** transport
attribute is not hosted — the developer wires it by hand.

## The Roslyn incremental generator

An `IIncrementalGenerator` (superior to the legacy `ISourceGenerator`: cached,
re-runs only for changed syntax nodes) performs three phases:

1. **Syntax provider** — cheap predicate finds type declarations implementing
   `IRequest<>`/`IQuery<>`/`ICommand` and their handlers.
2. **Semantic analysis** — resolves the symbol, response type, routing
   attributes and parameter sources against the compilation's semantic model for
   type safety.
3. **Transport emission** — for each transport the contract explicitly opted into
   (`[HttpEndpoint]`, `[GrpcMethod]`, `[RebusMessage]`), emits partial extension
   methods/services that wire the pure handler to that transport.

Emission is **opt-in and additive**: nothing is generated for a transport a
contract did not declare. When the framework is too limited for a single
request/query, the developer omits the attribute and hand-writes that transport
directly — the gRPC method inside the generated service `partial`, the Minimal
API `Map*` call, or the Rebus `IHandleMessages<>` — while still letting the
generator handle every other contract/transport.

### Emitted Minimal API

```csharp
app.MapPost("/api/v1/orders", async (
    CreateOrderRequest request, HttpContext ctx, CancellationToken ctk) =>
{
    // The SimpleInjector async scope is established once for the whole request by the
    // hosting pipeline (SimpleInjector integration middleware); the endpoint just resolves
    // the handler from that ambient scope — other middlewares share the same scope.
    var container = ctx.RequestServices.GetRequiredService<SimpleInjector.Container>();
    // the caller identity flows through IContextProvider<ClaimsPrincipal> (HttpContext.User)
    var handler = container.GetInstance<IRequestHandler<CreateOrderRequest, OrderResponse>>();
    var result = await handler.ExecuteAsync(request, ctk).ConfigureAwait(false);
    return Results.Ok(result);
});
```

The `IntroducedIn` and exclusive `RetiredIn` properties on `HttpEndpointAttribute`
expand a `{version}` route once per active version. The generator applies the
same lifetime rules to `[GrpcMethod]`, emitting one version-suffixed service per
`[ServiceGroup]` and retaining only methods active in that version.

For route/query-bound queries the generator emits `[FromRoute]`/`[FromQuery]`
parameters and reconstructs the request object before dispatch.

### Emitted gRPC (code-first)

`protobuf-net.Grpc` hosts a `[ServiceContract]` interface. The generator groups
the `[GrpcMethod]`-declared requests/queries (by namespace or
`[ServiceGroup("Orders")]`) into one generated `[ServiceContract]` interface plus
a `partial` implementation that resolves the pure handler from the ambient
request scope (opened by the SimpleInjector integration in the pipeline). The
`partial` lets a developer hand-write any method the generator cannot express.
Startup uses `AddCodeFirstGrpc()` + `MapGrpcService<OrdersService>()`.

NodaTime members on contracts serialize over protobuf via
[`Ark.Tools.Nodatime.Protobuf`](../../src/common/Ark.Tools.Nodatime.Protobuf)
(`RuntimeTypeModel.AddNodaTimeSurrogates()`). The wire mapping is layered:

- **Types natively supported by `NodaTime.Serialization.Protobuf`** use that
  library's conversions so the wire format is the corresponding Google
  well-known/common protobuf message. Hand-written encodings are **not** used
  for these types.
- **Custom surrogates exist only for types the library does not support.**

| NodaTime type | Wire representation | Provided by |
| --- | --- | --- |
| `Instant` | `google.protobuf.Timestamp` | `NodaTime.Serialization.Protobuf` (`ToTimestamp`/`ToInstant`) |
| `Duration` | `google.protobuf.Duration` | `NodaTime.Serialization.Protobuf` (`ToProtobufDuration`/`ToNodaDuration`) |
| `LocalDate` | `google.type.Date` | `NodaTime.Serialization.Protobuf` (`ToDate`/`ToLocalDate`) |
| `LocalTime` | `google.type.TimeOfDay` | `NodaTime.Serialization.Protobuf` (`ToTimeOfDay`/`ToLocalTime`) |
| `IsoDayOfWeek` | `google.type.DayOfWeek` | `NodaTime.Serialization.Protobuf` (`ToProtobufDayOfWeek`/`ToIsoDayOfWeek`) |
| `OffsetDateTime` | custom surrogate: local date-time + offset seconds | Ark surrogate |
| `LocalDateTime` | custom surrogate: date + nanosecond-of-day | Ark surrogate |
| `Period` | custom surrogate: ISO-8601 round-trip string | Ark surrogate |

protobuf-net does not serialize `Google.Protobuf` message classes directly, so
the surrogate structs for the natively-supported types mirror the well-known
message layout (same proto name and field numbers/types) and delegate the
semantic conversion to the `NodaTime.Serialization.Protobuf` extension methods.
Tests must cover **both** categories: the native mappings (`Instant`,
`Duration`, `LocalDate`, `LocalTime`, `IsoDayOfWeek`) and the custom surrogates
(`OffsetDateTime`, `LocalDateTime`, `Period`).

### Emitted Rebus handler

For request/command types marked `[RebusMessage]`, the generator emits
`IHandleMessages<CreateOrderRequest>` that invokes the pure handler. The
SimpleInjector scope is **not** opened by the wrapper: the Rebus pipeline already
establishes a per-message scope (`RebusScopeDecorator<>` over
`IHandleMessages<>`), which the wrapper and its dependencies resolve within. The
message/transport context is the unit of work / transaction boundary — no
`Rebus.UnitOfWork` is used.

### Why resolve from SimpleInjector explicitly

Native Minimal API / gRPC parameter injection uses the conforming container.
To keep the domain graph in SimpleInjector (lifestyle scoping, decorators,
startup verification, no captive dependencies), generated adapters fetch the
`SimpleInjector.Container` from `RequestServices` and resolve the handler from
the ambient request scope. That scope is opened once per request by the
SimpleInjector integration in the pipeline (and per message by the Rebus
pipeline), so adapters never open their own scope.

## Error handling

| Transport | Mechanism | Mapping |
| --- | --- | --- |
| Minimal API | `Hellang.Middleware.ProblemDetails` configured by the same `ArkProblemDetailsOptionsSetup` used in `Ark.Tools.AspNetCore` | `EntityNotFoundException`→404; `ValidationException`→400 + `extensions` field violations; `BusinessRuleViolationException`→400 with the violation payload in `extensions` |
| gRPC | server interceptor → `Google.Rpc.Status` rich error model | field violations packed as `BadRequest` details in trailing metadata; business rule violations packed as an `ArkBusinessRuleViolation` detail; thrown as `RpcException` |
| Rebus | scope disposal + native retry | exhausted → error/dead-letter queue with serialized exception headers |

Handlers only throw semantic domain exceptions; they never format transport
errors.

### HTTP: Hellang ProblemDetails + BusinessRuleViolation

The Minimal API host does **not** reimplement RFC 7807 mapping. It registers
`Hellang.Middleware.ProblemDetails` and reuses the mappings that
`Ark.Tools.AspNetCore` already ships (`ArkProblemDetailsOptionsSetup`):

- `EntityNotFoundException` → 404, `OptimisticConcurrencyException` → 409,
  `UnauthorizedAccessException` → 403, `ValidationException` → 400 with field
  violations.
- `BusinessRuleViolationException` → 400: the `BusinessRuleViolation`-derived
  object (class name = error code; extra public properties = structured data)
  is serialized into the ProblemDetails `extensions`, exactly as existing MVC
  hosts do. This behavior is preserved by the MVC-free host — clients observe
  the same payload.

### gRPC: BusinessRuleViolation over `Google.Rpc.Status`

Protobuf has no polymorphism, so a `BusinessRuleViolation`-derived object
cannot cross the wire as its concrete protobuf message. The design carries it
as a dedicated detail message inside the standard rich error model. The detail
message **mimics the RFC 7807 ProblemDetails base structure** — `type`,
`title`, `status`, plus the optional `detail` and `instance` — and carries the
violation's extra public properties in an `extensions` map whose **values only**
are JSON-encoded (no whole-object JSON blob):

```proto
message ArkBusinessRuleViolation {
  string type = 1;                    // violation class name (the error code)
  string title = 2;                   // BusinessRuleViolation.Title
  int32 status = 3;                   // HTTP-equivalent status (400)
  string detail = 4;                  // optional human-readable detail
  string instance = 5;                // optional occurrence URI/identifier
  map<string, string> extensions = 6; // extra public properties; each value JSON-encoded (ArkSerializerOptions)
}
```

- The mapping lives in the **library** (`Ark.Tools.MediatorFramework.Grpc`), as
  `ArkGrpcErrorInterceptor`, not in application code: the interceptor maps
  `BusinessRuleViolationException` to an `RpcException` with
  `StatusCode.FailedPrecondition` and a `Google.Rpc.Status` whose `details`
  contain one `ArkBusinessRuleViolation` (packed as `Any`), and
  `ValidationException` to `InvalidArgument` + `BadRequest` field violations.
- Each `extensions` entry is the JSON encoding of one derived-violation
  property (same `ArkSerializerOptions` JSON the HTTP ProblemDetails path
  emits per property), so a client can rebuild the derived violation when it
  knows the type and can always read `type`/`title`/`detail` generically.
- Polyglot clients that cannot parse the JSON values still get
  `type`/`title`/`detail`/`instance` as plain strings — the extension values
  are additive, never required.

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
- gRPC: an `IFormFile`-style single message **cannot** represent a file upload —
  a protobuf `bytes` field buffers the whole payload in memory and is capped by
  the max message size. Uploads use a **client-streaming** method instead:

```proto
service Documents {
  rpc Upload (stream UploadDocumentChunk) returns (UploadDocumentReply);
}

message UploadDocumentChunk {
  oneof content {
    UploadDocumentMetadata metadata = 1; // first message only
    bytes data = 2;                      // subsequent messages, ≤64KiB each
  }
}

message UploadDocumentMetadata {
  string name = 1;
  string content_type = 2;
}
```

  The generated service implementation consumes the
  `IAsyncEnumerable<UploadDocumentChunk>`: the first message must carry the
  metadata, every following message carries a data chunk. The chunks are
  exposed to the pure handler as an `IArkAttachment` whose `OpenRead()` returns
  a forward-only stream over the incoming sequence, so the handler code is
  identical for the HTTP multipart and the gRPC streaming path.

## API versioning

The version is part of the contract's **lifetime**, not of its route. A
request/query is available in every version from its introduction until its
retirement, mirroring how `Asp.Versioning` treats versions:

- The HTTP route template contains only the **placeholder**:
  `[HttpEndpoint("GET", "/api/v{version}/greetings/{id}")]`.
- The lifetime is declared on the transport attribute:
  `IntroducedIn` (int, required, e.g. `1`) and `RetiredIn` (int, optional,
  exclusive: the first version the contract is *not* part of).
- The host declares the set of versions it serves. For each hosted version `v`
  where `IntroducedIn <= v` and (`RetiredIn` unset or `v < RetiredIn`), the
  generator registers the route with `{version}` substituted (e.g.
  `/api/v1/greetings/{id}` **and** `/api/v2/greetings/{id}` from a single
  contract).
- OpenAPI documents are still partitioned per version; an endpoint appears in
  every document of a version it is active in.
- gRPC mirrors the same rule per `[ServiceGroup]`: one `[ServiceContract]`
  service is generated per active version (`GreetingsV1`, `GreetingsV2`, …),
  each containing the methods active in that version.
- Route parameters (`{id}` etc.) are ordinary `[FromRoute]` bindings and remain
  independent of the `{version}` placeholder; the sample must demonstrate and
  test both together.

Superseding a contract in a later version = retire the old contract at version
`n` and introduce the replacement with `IntroducedIn = n`.

## Polymorphism across transports

Each wire protocol has a native polymorphism mechanism; the design annotates
**one** contract hierarchy with all of them so a single pure handler serves
every transport:

| Protocol | Mechanism | Notes |
| --- | --- | --- |
| HTTP JSON | named discriminator property (`Ark.Tools.SystemTextJson.JsonPolymorphicConverter<TBase, TEnum>`) | human-readable; matches existing MVC hosts and OpenAPI `discriminator` |
| protobuf / gRPC | `[ProtoInclude(fieldNumber, typeof(Derived))]` on the base `[ProtoContract]` | protobuf-net inheritance: the base message embeds each subtype as a numbered optional sub-message (a de-facto `oneof`) |
| MessagePack | `[MessagePack.Union(key, typeof(Derived))]` on the base | key-numbered union — structurally the **same** approach as `ProtoInclude` |

Evaluation of the "protobuf way" (numbered subtype envelope) outside protobuf:

- **MessagePack: applicable.** `[Union]` is the idiomatic MessagePack
  equivalent — an integer key selects the subtype, exactly like a
  `ProtoInclude` field number. The sample uses matching numbers.
- **HTTP JSON: not adopted.** A numbered envelope (`{"1": {…}}`) is legal JSON
  but breaks human readability, OpenAPI `discriminator` support and existing
  Ark client conventions. JSON stays on the named discriminator property.

The sample's `Shape` hierarchy carries all three attribute sets and is
round-tripped through a generated Minimal API endpoint (JSON), a gRPC method
(`ProtoInclude`) and a MessagePack endpoint (`Union`), with a parity test
asserting the same handler result on each wire.

## `.proto` generation on build

The service IDL is C#; the `.proto` files are **build outputs**, produced by
the gRPC generator/package — no hand-maintained schema program:

- The gRPC incremental generator (which already knows every `[GrpcMethod]`
  contract and `[ProtoContract]` message) additionally emits the proto text as
  generated C# (`ArkGeneratedProtos`: per-`[ServiceGroup]` `(fileName, content)`
  pairs plus a `WriteTo(directory)` helper).
- `Ark.Tools.MediatorFramework.Grpc` ships a `buildTransitive` `.targets` file
  with an `ArkExportProto` target (`AfterTargets="Build"`, opt-in via
  `$(ArkExportProtoDir)`) that runs the built host with `--ark-export-proto
  <dir>`; the runtime helper `ArkProtoExport.TryHandle(args)` writes the files
  and short-circuits `Program`.
- Common messages are **shared, not repeated**, split over multiple files and
  `import`-ed by the per-service files:
  - `ark/nodatime.proto` (`LocalDate`, `LocalDateTime`, `OffsetDateTime`,
    `Period` surrogate messages) ships as a content asset of
    `Ark.Tools.Nodatime.Protobuf`;
  - `ark/mediator.proto` (`ArkBusinessRuleViolation`, `UploadDocumentChunk`,
    `UploadDocumentMetadata`) ships as a content asset of
    `Ark.Tools.MediatorFramework.Grpc`.
- Test/client projects consume the exported files with `Grpc.Tools`
  (`<Protobuf Include="…/*.proto" GrpcServices="Client" />`): the **client is
  generated from `.proto`**, never from the code-first C# contracts, proving
  wire compatibility for polyglot consumers.

## Rebus serialization

Protobuf over the bus uses the official `Rebus.Protobuf` package
(`.Serialization(s => s.UseProtobuf(typeModel))`) with a `RuntimeTypeModel`
pre-configured via `AddNodaTimeSurrogates()` — no bespoke `ISerializer`
implementation.

## HTTP hosting helpers

Recurring Minimal API hosting concerns are **library features**
(`Ark.Tools.MediatorFramework.MinimalApi`), not sample code:

- `AddArkNodaTimeSchemas()` — OpenAPI schema transformer mapping NodaTime
  types to `string` schemas with the Ark formats.
- `AddArkPolymorphism(...)` — registers a polymorphic hierarchy once
  (base type, discriminator property, `(discriminator value, derived type)`
  mapping — the same information Swashbuckle's
  `UseOneOfForPolymorphism`/`SelectSubTypesUsing` consume) and emits the
  OpenAPI `oneOf` + `discriminator` schema for it, so application developers
  never hand-write schema transformers.
- Multipart mapping: an extension that maps `IFormFile` → `ArkAttachment` and
  dispatches to the pure attachment handler, replacing the hand-written sample
  endpoint.
- MessagePack serde: `application/x-msgpack` request/response support for
  generated `[HttpEndpoint]` endpoints via content negotiation, demonstrated in
  the sample alongside the (escape-hatch) MVC controller.

## Composition: HTTP delegating to async Rebus work

The sample demonstrates transport composition, not just parity: an HTTP
handler completes the synchronous part of a workflow and **defers the rest to
the bus** by publishing a Rebus message from the handler (`IBus` injected into
the pure handler via the container); a `[RebusMessage]` handler completes the
workflow asynchronously. A behavioral test mutates over HTTP and polls a query
endpoint until the async effect is observable.

## Packaging: one package per transport

The framework ships as small packages, each dedicated to a single transport and
carrying **its own** incremental generator (bundled as an analyzer asset of the
same package):

| Package | Contents |
| --- | --- |
| `Ark.Tools.MediatorFramework` | transport-neutral core: `IArkAttachment`/`ArkAttachment`, shared versioning primitives |
| `Ark.Tools.MediatorFramework.MinimalApi` | `[HttpEndpoint]`, HTTP runtime helpers + the Minimal API endpoint generator |
| `Ark.Tools.MediatorFramework.Rebus` | `[RebusMessage]`, Rebus runtime helpers + the Rebus wrapper generator |
| `Ark.Tools.MediatorFramework.Grpc` | `[GrpcMethod]`/`[ServiceGroup]`, gRPC runtime helpers (interceptor, upload adapter) + the gRPC service generator |

An application references only the transports it hosts; each generator reacts
only to its own attribute, so adding a transport never re-runs the others.

## Testing strategy

- `samples/Ark.MediatorFramework.Sample/test/…Sample.Tests` demonstrates **how
  an application built on the framework is tested**: behavioral (BDD) tests
  written with **Reqnroll** (Gherkin feature files), exercising **only the
  public interfaces** — scenarios mutate and query state through the HTTP or
  gRPC endpoints (in-process `TestServer`/gRPC channel), never by reaching into
  handlers or stores — the pattern an adopting team copies.
- `tests/Ark.Tools.MediatorFramework.Tests` (repository `tests/` folder) tests
  the **framework capabilities themselves**: generator snapshot tests, attribute
  semantics (opt-in, versioning expansion), error-model mapping, attachment
  adapters — independent of the sample application. Every feature that lives in
  a `src/` library must be unit-tested here.

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
