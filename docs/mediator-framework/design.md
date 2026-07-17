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
- `[RebusMessage(OwnerQueue = "orders")]` — expose as a Rebus message and,
  when an owner is declared, contribute a type-based outbound route.

Generated HTTP endpoints require authorization by default. Set `Policy` on
`[HttpEndpoint]` to select a named policy, or set `AllowAnonymous = true` for
an intentionally public endpoint. `MapArkEndpoints` maps the generated routes
into one `RouteGroupBuilder`, invokes its optional configuration callback, and
returns the group so hosts can apply shared metadata such as authorization,
filters, rate limiting, CORS, or output caching.

Generator contract errors are reported at the transport attribute location:
`ARKMF010` (unknown HTTP verb), `ARKMF011` (unsupported handler kind),
`ARKMF012` (missing route property), `ARKMF013` (invalid HTTP contract shape),
`ARKMF014` (duplicate Rebus registration), and `ARKMF015` (conflicting Rebus
owner queue). Invalid contracts are not emitted.

HTTP binding treats the request as an **envelope** whose members may combine
route, query and body sources (see *HTTP binding* below). These attributes are
the only transport hint the developer writes; the generator turns each *declared*
transport into concrete hosting code. A request/query with **no** transport
attribute is not hosted — the developer wires it by hand.

Properties marked with `[ServerSet]` are never read from HTTP route, query, or
body input. The Minimal API generator resets them after deserialization, the
gRPC proto export omits them from request messages, and
`AddArkServerSetProperties()` removes them from generated schemas.

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
    if (result is null)
        return Results.NotFound();
    return Results.Ok(result);
});
```

Generated HTTP responses use these defaults: non-null queries and requests return
`200 OK`, null queries return `404 Not Found`, and null requests return
`204 No Content`. Set `SuccessStatusCode` or `NullResultStatusCode` on
`HttpEndpointAttribute` to override either code; the generated OpenAPI metadata
lists both response statuses.

Commands implement `ICommand` and are dispatched through `ICommandHandler<T>`.
HTTP-only commands execute inline and return `204 No Content`; commands also
marked with `[RebusMessage(OwnerQueue = "...")]` are sent to that queue and
return `202 Accepted`. Generated gRPC command methods return
`google.protobuf.Empty`, while Rebus wrappers invoke the command handler.

The `IntroducedIn` and exclusive `RetiredIn` properties on `HttpEndpointAttribute`
expand a `{version}` route once per active version. The generator applies the
same lifetime rules to `[GrpcMethod]`, emitting one version-suffixed service per
`[ServiceGroup]` and retaining only methods active in that version.

### HTTP binding: the request is always the envelope

A request/query is an **envelope**: its members can be sourced from the
**route**, the **query string** and the **body** *simultaneously*, mirroring
MVC model binding — and the contract stays a single pure type even when only a
body is present.

Binding rules applied by the generator, per property of the contract:

1. **Route** — a property whose name matches a route-template placeholder
   (`{id}` → `Id`) binds from the route. `{version}` is reserved for the
   version placeholder and never binds a property.
2. **Query** — for body-less verbs (GET/DELETE), every remaining property
   binds from the query string. For verbs with a body, a property opts into
   the query string with `[BindFromQuery]` (a transport-metadata attribute in
   the core package so Application assemblies don't reference ASP.NET).
3. **Body** — for verbs with a body, the payload deserializes into the
   envelope type itself (JSON or MessagePack per content negotiation); the
   route/query-bound properties are then **overwritten** from their sources.
   A body-only contract is therefore just the degenerate case where no
   property matched a placeholder or opted into the query.

The generator emits one lambda per endpoint whose parameters carry the
appropriate `[FromRoute]`/`[FromQuery]` sources plus the (optional) body, and
reconstructs the envelope (`init`-only members via object initializer /
`with`) before dispatching to the pure handler. All three sources are
demonstrated combined on a single sample contract and covered by tests.

### Generated multipart upload (single attachment)

Attachment uploads are ordinary envelopes too: a `[HttpEndpoint]` contract may
declare **at most one** `IArkAttachment` property. The generator (not
hand-written startup code) emits a `multipart/form-data` endpoint for it:
route and query properties bind per the envelope rules above, the single form
file maps to `ArkAttachment` and is assigned to the attachment property. More
than one `IArkAttachment` property is a generator diagnostic (error). The
`MapArkAttachmentUpload` runtime helper remains as the manual escape hatch
only. Generated multipart endpoints disable antiforgery explicitly because they
target bearer-token APIs; set `RequireAntiforgery = true` on the contract for a
cookie-authenticated host. `MaxRequestBodySizeBytes` adds a per-endpoint request
limit, and `AllowedContentTypes` rejects other file types with `415`. Filenames
are reduced to a leaf name and control characters are removed before the
attachment reaches a handler, including for streamed gRPC uploads.

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
- **All remaining date/time types map to `google.type.DateTime`**
  ([google/type/datetime.proto](https://github.com/googleapis/googleapis/blob/master/google/type/datetime.proto)),
  discriminated by its `time_offset` oneof — no bespoke wire shapes.
  `NodaTime.Serialization.Protobuf` does not cover `google.type.DateTime`, so
  the Ark surrogates implement that mapping themselves while keeping the exact
  common-proto layout.

| NodaTime type | Wire representation | Provided by |
| --- | --- | --- |
| `Instant` | `google.protobuf.Timestamp` | `NodaTime.Serialization.Protobuf` (`ToTimestamp`/`ToInstant`) |
| `Duration` | `google.protobuf.Duration` | `NodaTime.Serialization.Protobuf` (`ToProtobufDuration`/`ToNodaDuration`) |
| `LocalDate` | `google.type.Date` | `NodaTime.Serialization.Protobuf` (`ToDate`/`ToLocalDate`) |
| `LocalTime` | `google.type.TimeOfDay` | `NodaTime.Serialization.Protobuf` (`ToTimeOfDay`/`ToLocalTime`) |
| `IsoDayOfWeek` | `google.type.DayOfWeek` | `NodaTime.Serialization.Protobuf` (`ToProtobufDayOfWeek`/`ToIsoDayOfWeek`) |
| `LocalDateTime` | `google.type.DateTime` with **neither** `utc_offset` nor `time_zone` set | Ark surrogate (google.type layout) |
| `OffsetDateTime` | `google.type.DateTime` with `utc_offset` set | Ark surrogate (google.type layout) |
| `ZonedDateTime` | `google.type.DateTime` with `time_zone` set | Ark surrogate (google.type layout) |
| `Period` | custom surrogate: ISO-8601 round-trip string | Ark surrogate (no google.type equivalent) |

protobuf-net does not serialize `Google.Protobuf` message classes directly, so
every surrogate struct mirrors the well-known/common message layout exactly —
same proto package (`google.protobuf`/`google.type`), message name and field
numbers/types — and, where a `NodaTime.Serialization.Protobuf` conversion
exists, delegates the semantics to it. The `google.type.DateTime` surrogate
carries `year`..`nanos` (fields 1–7) and the `time_offset` oneof
(`utc_offset` = 8 as `google.protobuf.Duration`, `time_zone` = 9 as
`google.type.TimeZone`); which member of the oneof is populated (or none)
selects the NodaTime type on decode. Tests must cover **both** categories: the
native mappings (`Instant`, `Duration`, `LocalDate`, `LocalTime`,
`IsoDayOfWeek`) and the `google.type.DateTime`/`Period` surrogates
(`LocalDateTime` zoneless, `OffsetDateTime` offset preserved, `ZonedDateTime`
zone preserved, `Period` ISO string).

### Emitted Rebus handler

For request/command types marked `[RebusMessage]`, the generator emits
`IHandleMessages<CreateOrderRequest>` that invokes the pure handler. The
wrapper passes Rebus's ambient message-context cancellation token to the handler,
so bus shutdown and message-abort cancellation can reach in-flight work. The
SimpleInjector scope is **not** opened by the wrapper: the Rebus pipeline already
establishes a per-message scope (`RebusScopeDecorator<>` over
`IHandleMessages<>`), which the wrapper and its dependencies resolve within. The
message/transport context is the unit of work / transaction boundary — no
`Rebus.UnitOfWork` is used.

`RebusMessageAttribute.OwnerQueue` is an optional, non-empty queue name. The
Rebus generator emits one deterministic routing-map registration method for all
attributed message contracts that declare an owner. Hosting calls that method
inside `Routing(r => r.TypeBased()...)`; each entry is equivalent to Rebus
`Map<TMessage>(ownerQueue)`. Messages without an owner remain valid inbound-only
contracts and require explicit application routing before they can be sent.
Duplicate message mappings or conflicting owners are compile-time diagnostics;
the generator never silently picks one. Queue ownership is routing metadata
only: it does not create queues, configure a transport, or alter generated
`IHandleMessages<T>` wrappers.

### Why resolve from SimpleInjector explicitly

Native Minimal API / gRPC parameter injection uses the conforming container.
To keep the domain graph in SimpleInjector (lifestyle scoping, decorators,
startup verification, no captive dependencies), generated adapters fetch the
`SimpleInjector.Container` from `RequestServices` and resolve the handler from
the ambient request scope. That scope is opened once per request by the
SimpleInjector integration in the pipeline (and per message by the Rebus
pipeline), so adapters never open their own scope.

Generated transport registration methods perform a separate, targeted
handler-registration lookup at startup. Missing closed handler services are
aggregated into one actionable exception naming each contract and interface;
this check does not invoke or duplicate SimpleInjector's `Verify()` dependency
graph validation.

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

- Minimal API: the **generated** multipart endpoint accepts `IFormFile` and
  maps it into `ArkAttachment`, binding route/query envelope members alongside
  the single attachment (see *Generated multipart upload*).
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
- Hosts with hand-written protobuf services can declare
  `<ArkAdditionalProto Include="path/to/service.proto" />`; the export target
  copies those files alongside generated service protos without making them
  part of the framework generator.
- `Ark.Tools.MediatorFramework.Grpc` ships a `buildTransitive` `.targets` file
  with an `ArkExportProto` target (`AfterTargets="Build"`, opt-in via
  `$(ArkExportProtoDir)`) that runs the built host with `--ark-export-proto
  <dir>`; the runtime helper `ArkProtoExport.TryHandle(args)` writes the files
  and short-circuits `Program`.
- Common messages are **shared, not repeated**, split over multiple files and
  `import`-ed by the per-service files. Shared files declare the proto
  `package` and `option csharp_namespace` of their **owning library** — never
  an application namespace:
  - `ark/nodatime.proto` (`package ark.nodatime;`,
    `option csharp_namespace = "Ark.Tools.Nodatime.Protobuf"`) ships as a
    content asset of `Ark.Tools.Nodatime.Protobuf`. It contains **only** the
    types without a Google common proto: the `Period` message and the
    `google.type.DateTime`-based mappings are expressed by importing
    `google/type/datetime.proto` (and `google/type/date.proto` etc. where
    referenced) — a `LocalDate` message must **not** be redefined, the native
    `google.type.Date` is used.
  - `ark/mediator.proto` (`package ark.mediator;`,
    `option csharp_namespace = "Ark.Tools.MediatorFramework.Grpc"`) with
    `ArkBusinessRuleViolation`, `UploadDocumentChunk`,
    `UploadDocumentMetadata` ships as a content asset of
    `Ark.Tools.MediatorFramework.Grpc`.
- The export target and `$(ArkExportProtoDir)` wiring are **not** authored in
  the consuming csproj: `Ark.Tools.MediatorFramework.Grpc` delivers them via
  `buildTransitive` `.props`/`.targets` that NuGet imports automatically from
  the package manifest (project-reference consumers inside this repo get the
  same import through the package's MSBuild assets). If csproj-driven packing
  cannot express the layout, a dedicated
  `Ark.Tools.MediatorFramework.Grpc.MsBuild` package (nuspec-authored) carries
  the MSBuild assets instead.
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

- `AddArkNodaTimeSchemas()` — OpenAPI schema transformer covering **every**
  NodaTime type that `Ark.Tools.AspNetCore.Swashbuckle`'s
  `SupportNodaTimeExtensions` maps today: `LocalDate`, `LocalDateTime`,
  `Instant`, `OffsetDateTime`, `ZonedDateTime`, `LocalTime`, `DateTimeZone`
  and `Period` (nullable forms included), with the same formats/examples.
- `AddArkPolymorphism(...)` — registers a polymorphic hierarchy once
  (base type, discriminator property, `(discriminator value, derived type)`
  mapping — the same information Swashbuckle's
  `UseOneOfForPolymorphism`/`SelectSubTypesUsing` consume) and emits the
  OpenAPI `oneOf` + `discriminator` schema for it, so application developers
  never hand-write schema transformers.
- Multipart mapping: generated from the contract (see *Generated multipart
  upload*); `MapArkAttachmentUpload` stays as the manual escape hatch.
- MessagePack serde: **no specialized `Map*` method.** The `[HttpEndpoint]`
  attribute declares which serializations the endpoint supports (JSON and/or
  MessagePack, JSON being the default); the generator emits a single mapped
  endpoint that **content-negotiates** between `application/json` and
  `application/x-msgpack` on both the request `Content-Type` and the `Accept`
  header. When MessagePack is enabled, an `IFormatterResolver` **must** be
  registered in DI and configured explicitly — there is **no** built-in
  fallback resolver; generated endpoint mapping validates every enabled contract
  at startup and aggregates missing formatter failures. Request deserialization
  uses `MessagePackSecurity.UntrustedData`; response serialization keeps the
  configured resolver's normal security options.

The HTTP hosting stack tracks the current ASP.NET Core release only:
`Ark.Tools.MediatorFramework.MinimalApi` (runtime and generator output)
targets **`net10.0` exclusively** — `[HttpEndpoint]` hosting has no `net8.0`
support. The transport-neutral core and the Rebus/gRPC packages keep the
repo-wide multi-targeting.

## Developer-facing transport UIs

The sample exposes the versioned OpenAPI documents as it does today and adds
**Scalar as the primary UI**. Swagger UI remains an opt-in compatibility UI for
teams needing its established OAuth2/OIDC configuration. Both consume the same
generated documents, are mapped unconditionally in every environment and are
explicitly marked anonymous — business endpoints are protected by default, so
anonymous doc UIs expose no protected operation — and must model
authentication in the OpenAPI document. Authorization-code with PKCE is the
default interactive flow; client secrets are forbidden in browser
configuration. A test verifies each UI route references both versioned
documents, while OpenAPI snapshot tests verify the OAuth2/OpenID Connect
security schemes independently of either renderer.

For gRPC, the supported browser experience is **gRPCui**, both as a development
tool and as a production operations panel. The sample documents a launch command
that points gRPCui at the build-exported `.proto` tree, so unary and streaming
operations remain discoverable without enabling reflection. gRPC endpoints use
the host's bearer authentication policy in every environment. Operators pass an
OAuth2 access token as authorization metadata; gRPCui does not perform the OAuth2
login or token refresh itself. Optional ASP.NET Core gRPC reflection is mapped unconditionally and explicitly
marked anonymous for the same reason. The
framework does not embed or proxy gRPCui and does not translate gRPC into HTTP,
keeping gRPC wire behavior and streaming semantics authoritative.

## System.Text.Json source generation

The sample demonstrates STJ metadata source generation with a
`JsonSerializerContext` covering every HTTP request/response root and reachable
polymorphic subtype. Its generated resolver is inserted before the reflection
resolver in the `HttpJsonOptions` resolver chain after applying Ark defaults, so
custom NodaTime, enum and discriminator converters keep the existing wire
shape. Framework code continues to consume the configured
`JsonSerializerOptions`; it does not reference the sample context or create
private serializer options. Behavioral tests exercise generated HTTP endpoints
and fail if a contract falls back because it is missing from the context. This
is sample-level Native AOT preparation, not a promise that the full host is yet
trim-safe.

## Composition: HTTP delegating to async Rebus work

The sample demonstrates transport composition, not just parity: an HTTP
handler completes the synchronous part of a workflow and **defers the rest to
the bus** by publishing a Rebus message from the handler (`IBus` injected into
the pure handler via the container); a `[RebusMessage]` handler completes the
workflow asynchronously. A behavioral test mutates over HTTP and polls a query
endpoint until the async effect is observable.

## Transport-agnostic authorization

HTTP and gRPC authorization metadata protects their respective edges, but it does
not protect a handler reached through Rebus. Contracts exposed on more than one
transport can declare a shared `PolicyAuthorizeAttribute`; the
`Ark.Tools.Solid.Authorization` registration helper decorates query, request and
command handlers with the policy check. The decorator resolves the current
`ClaimsPrincipal` from `IContextProvider<ClaimsPrincipal>` and therefore applies
the same policy to HTTP, gRPC and Rebus dispatch. Hosts must register the
authorization services, policy provider and decorators once in the composition
root, and register every named policy used by contracts.

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

### Dependency flow and sample consumption

- **Transitive dependencies stay in the packages.** Everything a transport
  needs (`protobuf-net.Grpc.AspNetCore`, `Grpc.StatusProto`,
  `System.ServiceModel.Primitives`, `Rebus.Protobuf`,
  `Hellang.Middleware.ProblemDetails`, `MessagePack`,
  `Microsoft.AspNetCore.OpenApi`, …) is a dependency of the corresponding
  `Ark.Tools.MediatorFramework.*` package and flows transitively; the
  application host csproj lists **only** the `Ark.Tools.MediatorFramework.*`
  packages (plus app-specific packages), never the underlying stack.
- **The sample consumes the framework like an application would.** Mirroring
  `Ark.ReferenceProject`'s version-replacement trick, the sample references
  the framework via `PackageReference` with a central `PackageVersion` of
  `999.9.9` (the local development `$(Version)` from the root
  `Directory.Build.props`): inside the repo solution NuGet resolves these to
  the sibling projects (`"type": "Project"` in the lock file); on eject the
  same csproj resolves to the published packages. No relative
  `ProjectReference` paths — including the analyzers, which reach the sample
  as analyzer assets of their runtime package.

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

## Build discipline

Every implementation step — regardless of how small — ends by building the
**entire solution** (`dotnet build Ark.Tools.slnx --configuration Debug`) and
running the affected tests before committing. A step is not complete while any
project in the solution fails to build.

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
