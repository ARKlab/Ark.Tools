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

## Next implementation order

1. **T7.3** Document migration from MVC, including when to retain a
   hand-written adapter such as the MessagePack endpoint.

## Status legend

- `[x]` implemented and self-tested in this PR.
- `[ ]` specified with acceptance criteria; scheduled per
  [`implementation-plan.md`](implementation-plan.md).
