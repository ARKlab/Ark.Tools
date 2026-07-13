# Task breakdown (verifiable)

Every task lists an explicit, testable acceptance criterion. "Green" means the
project builds under the repo's strict settings and its self-tests pass with
`dotnet test`.

## Epic 1 — Pure handlers + DI

- [x] **T1.1** Sample solution `samples/Ark.MediatorFramework.Sample` created and
  added to the build.
  - *Accept:* `dotnet build` succeeds for the new solution.
- [x] **T1.2** Pure `IRequest`/`IQuery` contracts + handlers (no transport types).
  - *Accept:* handler source references no `HttpContext`/`ServerCallContext`/
    `MessageContext` (asserted by a test scanning the compiled types).
- [x] **T1.3** SimpleInjector container with an audit/logging decorator applied to
  all handlers.
  - *Accept:* a test asserts the decorator executed exactly once per dispatch on
    every transport.

## Epic 2 — Minimal API transport

- [x] **T2.1** Minimal API endpoints dispatch to pure handlers through the
  per-request SimpleInjector async scope established once in the hosting pipeline
  (not re-opened per endpoint).
  - *Accept:* an HTTP self-test (`TestServer`) posts a request and asserts the
    handler result.
- [x] **T2.2** Roslyn incremental generator discovers handlers marked with the
  explicit, opt-in `[HttpEndpoint]` attribute and emits the endpoint-registration
  extension method.
  - *Accept:* the registration invoked at runtime is generated code
    (`[GeneratedCode]`); a generator test asserts expected `MapPost/MapGet`
    output; the HTTP self-test still passes using the generated registration.

## Epic 3 — Rebus transport

- [x] **T3.1** Generated/adapter `IHandleMessages<T>` wrapper (opt-in via
  `[RebusMessage]`) invokes the same pure handler within the SimpleInjector scope
  opened by the Rebus pipeline (`RebusScopeDecorator<>`, no `Rebus.UnitOfWork`)
  over the in-memory transport.
  - *Accept:* a self-test sends a Rebus message and asserts the handler produced
    the same result as the HTTP path.

## Epic 4 — Code-first gRPC transport

- [x] **T4.1** `[ProtoContract]` contracts + generated `[ServiceContract]` service
  (opt-in via `[GrpcMethod]`, grouped by `[ServiceGroup]`), hosted with
  `AddCodeFirstGrpc()`; the generated service is `partial` for manual methods.
  - *Accept:* an in-process `Grpc.Net.Client` self-test calls the service and
    gets the same result as the HTTP path.
- [x] **T4.2** `.proto` emission MSBuild target.
  - *Accept:* `dotnet build` writes a proto3 file whose service/messages match
    the C# contracts (asserted by a test reading the emitted file).
- [x] **T4.2a** Generate a client assembly from the emitted `.proto` for the
  behavioral tests.
  - *Accept:* the gRPC behavioral test uses the generated client stub and
    generated message types rather than the code-first client contract.
- [x] **T4.3** gRPC rich-error-model interceptor.
  - *Accept:* a self-test throws `ValidationException` and asserts the generated
    proto client receives a `Google.Rpc.Status` with `BadRequest` field
    violations.
- [x] **T4.4** NodaTime protobuf surrogates for contracts crossing the gRPC wire.
  - *Accept:* `Ark.Tools.Nodatime.Protobuf` round-trips `OffsetDateTime` (offset
    preserved), `LocalDate` (date only), `LocalDateTime` (zoneless) and `Period`
    (ISO string) through protobuf-net, asserted by tests.

## Epic 5 — Cross-cutting concerns

- [x] **T5.1** Caller identity exposed via `IContextProvider<ClaimsPrincipal>`
  per transport and injected into handlers.
  - *Accept:* the Minimal API self-test dispatches through the handler which reads
    the identity via `IContextProvider<ClaimsPrincipal>` (AspNetCore auth, with
    Rebus propagation on the bus transport).
- [x] **T5.2** Minimal API `ProblemDetails` (RFC 7807) mapping.
  - *Accept:* `EntityNotFoundException`→404 and `ValidationException`→400 with
    field violations in `extensions`, asserted over HTTP.
- [x] **T5.3** Rebus dead-letter behavior on exhausted retries.
  - *Accept:* a self-test forces a failing handler and asserts the message lands
    in the error queue with the exception in headers.

## Epic 6 — OpenAPI & attachments

- [x] **T6.1** `Microsoft.AspNetCore.OpenApi` document generated from endpoints.
  - *Accept:* a snapshot test validates the generated document contains the
    expected paths/schemas.
  - *Also:* per-version OpenAPI documents (`/openapi/v1.json`, `/openapi/v2.json`)
    partition endpoints by the API version the generator infers from the route
    template, and System.Text.Json polymorphic contracts (via the shared
    `Ark.Tools.SystemTextJson.JsonPolymorphicConverter`) round-trip through a
    generated endpoint — both asserted by tests.
- [x] **T6.2** `IFormFile` → `IArkAttachment` attachment mapping.
  - *Accept:* a multipart upload self-test asserts the handler received the
    stream content.

- [x] **T6.3** MessagePack compatibility endpoint for an existing pure handler.
  - *Accept:* a content-negotiated `application/x-msgpack` request and response
    round-trip NodaTime values through the hand-written MVC adapter without
    changing the handler.

## Epic 7 — Productization

- [x] **T7.1** Extract runtime + generator to `src/` packages with XML docs.
- [x] **T7.2** `packages.lock.json` committed for every new project
  (CI `RestoreLockedMode`).
- [x] **T7.3** Migration guide from MVC controllers.
  - *Accept:* docs reviewed; package validation + SBOM succeed in CI.

## Epic 8 — Review revisions (2026-07 review)

- [x] **T8.1** NodaTime protobuf: adopt `NodaTime.Serialization.Protobuf` for
  natively supported types; keep custom surrogates only for the rest.
  - *Accept:* `Instant`, `Duration`, `LocalDate`, `LocalTime` and
    `IsoDayOfWeek` round-trip through protobuf-net using the library's
    conversions with well-known/common-proto wire shapes
    (`google.protobuf.Timestamp`, `google.protobuf.Duration`,
    `google.type.Date`, `google.type.TimeOfDay`, `google.type.DayOfWeek`);
    `OffsetDateTime`, `LocalDateTime` and `Period` round-trip via the custom
    surrogates; tests cover **both** categories.
- [x] **T8.2** Minimal API errors via Hellang ProblemDetails, including
  `BusinessRuleViolation`.
  - *Accept:* the sample host uses `Hellang.Middleware.ProblemDetails` with
    the `Ark.Tools.AspNetCore` mappings; an HTTP self-test throws
    `BusinessRuleViolationException` from a pure handler and asserts a 400
    whose `extensions` contains the derived violation payload (same shape as
    existing MVC hosts).
- [x] **T8.3** gRPC `BusinessRuleViolation` mapping.
  - *Accept:* the interceptor maps `BusinessRuleViolationException` to
    `RpcException` (`FailedPrecondition`) carrying a `Google.Rpc.Status` with
    an `ArkBusinessRuleViolation` detail (`type`, `title`, `status`,
    `payload_json`); a generated-proto client test reads the detail and
    deserializes the payload JSON.
- [x] **T8.4** gRPC file upload (client streaming).
  - *Accept:* a client-streaming method (metadata-first chunked messages) is
    generated/hosted; a self-test streams a payload larger than one chunk and
    asserts the *same* pure attachment handler received the full content via
    `IArkAttachment.OpenRead()`.
- [x] **T8.5** Version lifetime (introduced/retired) with `{version}` route
  placeholder.
  - *Accept:* `[HttpEndpoint]` routes declare only `/api/v{version}/…`;
    `IntroducedIn`/`RetiredIn` on the attribute drive generation of one route
    per active version; self-tests call the same contract on two versions and
    assert a retired contract is absent from later versions; a route-parameter
    (`{id}`) test passes on every generated version; gRPC generates one
    service per active version per `[ServiceGroup]`.
- [x] **T8.6** Split the framework into per-transport packages, each with its
  own `netstandard2.0` analyzer and transport-only generator output.
  - *Accept:* `Ark.Tools.MediatorFramework` (core) plus
    `…MediatorFramework.MinimalApi`, `…MediatorFramework.Rebus` and
    `…MediatorFramework.Grpc`, each bundling its own analyzer; the sample
    references only the packages for the transports it hosts; solution builds
    with locked-mode restore (lock files updated).
- [x] **T8.7** Framework capability tests under `tests/`.
  - *Accept:* `tests/Ark.Tools.MediatorFramework.Tests` exercises the
    generators (snapshot tests) and runtime pieces independent of the sample;
    the sample `…Sample.Tests` remains the "how to test an application"
    demonstration.

## Epic 9 — Review revisions (2026-07 second review)

- [x] **T9.1** Move the gRPC error interceptor into
  `Ark.Tools.MediatorFramework.Grpc` and reshape `ArkBusinessRuleViolation`
  after ProblemDetails.
  - *Accept:* `ArkGrpcErrorInterceptor` lives in the Grpc library (sample only
    registers it); `ArkBusinessRuleViolation` has `type`=1, `title`=2,
    `status`=3, `detail`=4, `instance`=5 and `map<string,string> extensions`=6
    where **only** the extension values are JSON-encoded (no whole-object
    `payload_json`); unit tests in `tests/Ark.Tools.MediatorFramework.Tests`
    cover the wire shape and both exception mappings; the sample gRPC client
    test reads `detail`/`instance`/`extensions`.
- [x] **T9.2** Adopt the official `Rebus.Protobuf` serializer.
  - *Accept:* the sample's hand-written `ProtobufRebusSerializer` is deleted;
    Rebus is configured with `.Serialization(s => s.UseProtobuf(typeModel))`
    where the `RuntimeTypeModel` has `AddNodaTimeSurrogates()` applied; the
    existing protobuf-over-Rebus test still passes.
- [x] **T9.3** HTTP hosting helpers in `Ark.Tools.MediatorFramework.MinimalApi`.
  - *Accept:* `AddArkNodaTimeSchemas()` (OpenAPI NodaTime schema transformer),
    `AddArkPolymorphism(...)` (one registration per hierarchy → OpenAPI
    `oneOf` + `discriminator`, modeled on Swashbuckle's
    `UseOneOfForPolymorphism`/`SelectSubTypesUsing` inputs) and the
    `IFormFile`→`ArkAttachment` multipart mapping extension are library APIs
    with XML docs; the sample startup uses them and contains no hand-written
    schema transformer or multipart endpoint; unit tests in `tests/` cover the
    helpers.
- [x] **T9.4** Cross-transport polymorphism demonstration.
  - *Accept:* the `Shape` hierarchy carries `[ProtoContract]`+`[ProtoInclude]`
    (gRPC), `[MessagePack.Union]` (MessagePack) and the existing STJ
    discriminator converter (JSON) with matching subtype numbers; a gRPC method
    and a MessagePack round-trip exercise the polymorphic contract; a parity
    test asserts the same handler result on all three wires; `design.md`'s
    applicability evaluation (protobuf-way OK for MessagePack, rejected for
    JSON) is reflected in sample comments.
- [x] **T9.5** MessagePack serde on a generated Minimal API endpoint.
  - *Accept:* a `[HttpEndpoint]` contract round-trips
    `application/x-msgpack` request **and** response (content negotiation)
    through the Minimal API pipeline using the MinimalApi library helper; the
    MVC `MessagePackGreetingController` remains only as the documented
    escape-hatch demo; a self-test covers msgpack-in/msgpack-out including
    NodaTime values.
- [x] **T9.6** `.proto` generated on build as assets; shared protos split per
  package; delete `ProtoGenerator`.
  - *Accept:* the gRPC generator emits `ArkGeneratedProtos` (per
    `[ServiceGroup]` file content importing `ark/nodatime.proto` and
    `ark/mediator.proto`); `Ark.Tools.Nodatime.Protobuf` packs
    `ark/nodatime.proto` and `Ark.Tools.MediatorFramework.Grpc` packs
    `ark/mediator.proto` as content assets; the Grpc package ships a
    `buildTransitive` target exporting the files after `Build` (opt-in
    `$(ArkExportProtoDir)`, `ArkProtoExport.TryHandle(args)` runtime helper);
    `samples/…/Ark.MediatorFramework.ProtoGenerator` is deleted;
    `…Sample.GrpcClient` generates the client from the **exported** `.proto`
    via `Grpc.Tools` and all gRPC tests pass; a snapshot test in `tests/`
    validates the emitted proto text.
- [ ] **T9.7** Sample composition: HTTP handler delegating async work to Rebus.
  - *Accept:* an HTTP-exposed handler publishes a `[RebusMessage]` message
    (`IBus` injected into the pure handler) whose Rebus handler completes the
    workflow; a test mutates over HTTP and polls a query endpoint until the
    async effect is visible.
- [ ] **T9.8** Reqnroll behavioral tests for the sample.
  - *Accept:* `…Sample.Tests` gains Gherkin feature files (Reqnroll.MsTest)
    covering create/query greetings, the business-rule violation, versioning
    and the HTTP→Rebus composition; step definitions call only HTTP/gRPC
    interfaces (no direct handler/store access); existing capability tests may
    remain as plain MSTest where they test transport plumbing.
- [ ] **T9.9** Refinement sweep: framework features out of the sample.
  - *Accept:* after T9.1–T9.8 the sample's `WebInterface` contains only
    composition/wiring and hand-written escape-hatch demos; every reusable
    piece (interceptor, upload adapter, OpenAPI helpers, multipart mapping,
    proto export) lives in a `src/` package and has unit tests in
    `tests/Ark.Tools.MediatorFramework.Tests`; docs updated.

## Next implementation order

1. **T9.1** gRPC interceptor to library + ProblemDetails-shaped detail.
2. **T9.2** `Rebus.Protobuf` adoption.
3. **T9.3** MinimalApi hosting helpers.
4. **T9.4** Cross-transport polymorphism.
5. **T9.5** MessagePack serde on Minimal API.
6. **T9.6** Proto-on-build + shared proto assets (after T9.1 so
   `ark/mediator.proto` matches the final detail shape).
7. **T9.7** HTTP→Rebus composition.
8. **T9.8** Reqnroll behavioral tests.
9. **T9.9** Refinement sweep.

## Status legend

- `[x]` implemented and self-tested in this PR.
- `[ ]` specified with acceptance criteria; scheduled per
  [`implementation-plan.md`](implementation-plan.md).
