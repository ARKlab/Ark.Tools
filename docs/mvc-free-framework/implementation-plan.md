# Implementation plan

The framework is delivered in phases. Each phase ends with a **verifiable**
increment: it builds under the repo's strict settings (`TreatWarningsAsErrors`,
full analyzer set) and its self-tests pass with `dotnet test`.

## Packages to introduce

New third-party dependencies required by the full framework (subject to the
repo's "no new dependency without approval" rule; the proof-of-concept sample
minimizes these):

| Package | Role | Phase |
| --- | --- | --- |
| `Microsoft.CodeAnalysis.CSharp` | Roslyn incremental generator (build-time analyzer, `PrivateAssets=all`) | 2 |
| `protobuf-net.Grpc.AspNetCore` | Code-first gRPC hosting | 3 |
| `protobuf-net.Grpc.Reflection` | `.proto` emission for polyglot consumers | 3 |
| `Grpc.Net.Client` | In-process gRPC client for self-tests | 3 |
| `Grpc.Tools` | Generate a client assembly from the emitted `.proto` for behavioral tests | 3 |
| `Microsoft.AspNetCore.OpenApi` | OpenAPI from Minimal API metadata | 4 |
| `NodaTime.Serialization.Protobuf` | Canonical NodaTime↔well-known-proto conversions (requested in review) | 6 |
| `Google.Api.CommonProtos` | `google.type.Date`/`TimeOfDay`/`DayOfWeek` types used by the above | 6 |

Already available in the repo: `SimpleInjector`, `Rebus`, `Ark.Tools.Solid*`,
`Ark.Tools.SimpleInjector`, `Ark.Tools.Rebus`, `Hellang.Middleware.ProblemDetails`
(via `Ark.Tools.AspNetCore`), MSTest + Microsoft.Testing.Platform.

## Phase 1 — Pure handlers + Minimal API + Rebus (proof of concept)

- Create the sample solution `samples/Ark.MediatorFramework.Sample`.
- Define pure `IRequest`/`IQuery` contracts and handlers (`Ark.Tools.Solid`).
- Wire SimpleInjector with a cross-cutting decorator (audit/logging) to prove
  decorators apply regardless of transport.
- Host the handlers over ASP.NET Core Minimal APIs (hand-written registration
  first, to establish the runtime contract the generator will later emit).
- Host the same handlers over Rebus using the in-memory transport.
- Self-tests: call each transport and assert identical results, and assert the
  decorator ran.

**Verifiable:** `dotnet test` green; both transports dispatch the same handler.

## Phase 2 — Roslyn incremental source generator

- Add the generator project targeting `netstandard2.0` referencing
  `Microsoft.CodeAnalysis.CSharp`.
- Discover handler/request types via the syntax + semantic pipeline.
- Emit the Minimal API endpoint-registration extension method (replacing the
  hand-written Phase-1 registration).
- Snapshot/generator tests assert the generated source and that the generated
  registration produces working endpoints.

**Verifiable:** the endpoint registration used at runtime is generated code;
generator tests + transport tests green.

## Phase 3 — Code-first gRPC transport

- Add `protobuf-net.Grpc.AspNetCore`; annotate contracts with
  `[ProtoContract]`/`[ProtoMember]`.
- Generate the `[ServiceContract]` interface + implementation per
  `[ServiceGroup]`, resolving handlers from SimpleInjector.
- Add a `.proto` emission MSBuild target for polyglot consumers.
- Generate a client assembly from the emitted `.proto` and use it from the
  behavioral tests, so the gRPC test exercises the published wire contract.
- Self-test with the generated client over an in-process `Grpc.Net.Client`
  channel hitting the same handler; add the gRPC rich-error-model interceptor
  and test the mapping.

**Verifiable:** gRPC call and HTTP call to the same handler return identical
results; error interceptor maps `ValidationException` to `Google.Rpc.Status`.

## Phase 4 — Cross-cutting, errors, OpenAPI, attachments

- `IContextProvider<ClaimsPrincipal>` wiring per transport (AspNetCore auth +
  Rebus propagation) + tests.
- `ProblemDetails` (Minimal API) and dead-letter behavior (Rebus) tests.
- `Microsoft.AspNetCore.OpenApi` document generation + snapshot test.
- `IFormFile`/`IArkAttachment` attachment mapping + streaming test.
- `Ark.Tools.Nodatime.Protobuf` surrogates for NodaTime types on gRPC contracts.

**Verifiable:** each concern has a dedicated passing self-test.

## Phase 5 — Productization

- Extract the runtime support + per-transport generators into `src/` packages
  (`Ark.Tools.MediatorFramework` and the Minimal API, Rebus and gRPC packages).
- Add `packages.lock.json` for all new projects (CI runs `RestoreLockedMode`).
- Package validation, SBOM, XML docs on all public APIs.
- Migration guidance for existing MVC controllers.

## Phase 6 — Review revisions (step-by-step, executable)

Execute the steps **in order**; each step is independently verifiable. General
rules for every step: build with `dotnet build Ark.Tools.slnx --configuration
Debug`; run the affected test project with `dotnet test <project> -f net10.0`;
when a `PackageReference` changes, add the version to
`Directory.Packages.props` (CPM, no `VersionOverride`) and refresh lock files
with `dotnet restore Ark.Tools.slnx --force-evaluate`; commit per step with a
Conventional Commit message.

### Step 6.1 — NodaTime protobuf revision (T8.1)

1. Add `NodaTime.Serialization.Protobuf` and `Google.Api.CommonProtos` to
   `Directory.Packages.props` and reference them from
   `src/common/Ark.Tools.Nodatime.Protobuf/Ark.Tools.Nodatime.Protobuf.csproj`.
2. In `src/common/Ark.Tools.Nodatime.Protobuf/Surrogates/` add surrogates for
   `Instant`, `Duration`, `LocalTime` and `IsoDayOfWeek`, and **rewrite**
   `LocalDateSurrogate`:
   - each surrogate is a `[ProtoContract]` struct whose proto name and field
     numbers/types mirror the well-known/common message
     (`google.protobuf.Timestamp`: `seconds`=1 int64, `nanos`=2 int32;
     `google.protobuf.Duration`: same; `google.type.Date`: `year`=1,
     `month`=2, `day`=3; `google.type.TimeOfDay`: `hours`..`nanos`=1..4;
     `google.type.DayOfWeek` maps as enum value);
   - conversion operators delegate to `NodaTime.Serialization.Protobuf`
     (`ToTimestamp`/`ToInstant`, `ToProtobufDuration`/`ToNodaDuration`,
     `ToDate`/`ToLocalDate`, `ToTimeOfDay`/`ToLocalTime`,
     `ToProtobufDayOfWeek`/`ToIsoDayOfWeek`) — do **not** hand-roll the math.
3. Register the new surrogates in `Ex.AddNodaTimeSurrogates` (keep the
   idempotency guard). Keep `OffsetDateTime`, `LocalDateTime`, `Period`
   surrogates as-is.
4. Extend `tests/Ark.Tools.Nodatime.Protobuf.Tests`: one round-trip test per
   type, grouped in two classes — `NativeConversionTests` (Instant, Duration,
   LocalDate, LocalTime, IsoDayOfWeek) and `SurrogateTests` (OffsetDateTime,
   LocalDateTime, Period). Also assert the emitted schema
   (`model.GetSchema(...)`) names the well-known types for the native group.
5. Update the table in `design.md` if the implementation diverges; verify the
   emitted sample `.proto` still matches its test.

### Step 6.2 — Hellang ProblemDetails + BusinessRuleViolation over HTTP (T8.2)

1. In `samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface`
   replace the hand-written exception→ProblemDetails mapping with
   `Hellang.Middleware.ProblemDetails` configured via the existing
   `ArkProblemDetailsOptionsSetup` (see
   `src/aspnetcore/Ark.Tools.AspNetCore/ProblemDetails/ArkProblemDetailsOptionsSetup.cs`
   and its usage in
   `samples/Ark.ReferenceProject/Core/Ark.Reference.Core.WebInterface/Startup.cs`).
   Reference only the ProblemDetails pieces — do not pull the MVC startup base
   classes into the MVC-free sample; if that is not possible without
   referencing all of `Ark.Tools.AspNetCore`, extract the setup into a small
   shared source or replicate the mapping table 1:1 with a comment pointing at
   the origin.
2. Add a sample violation type (e.g. `GreetingAlreadyExistsViolation :
   BusinessRuleViolation` in `…Sample.Application`) and throw
   `BusinessRuleViolationException` from a handler path reachable via HTTP.
3. Test in `…Sample.Tests`: POST triggering the violation returns 400,
   `content-type: application/problem+json`, and the extensions contain the
   violation payload (`type` = class name, custom properties present).
4. Keep the existing 404/validation tests green (mapping must not regress).

### Step 6.3 — gRPC BusinessRuleViolation detail (T8.3)

1. Define the `ArkBusinessRuleViolation` protobuf contract (fields: `type`=1,
   `title`=2, `status`=3, `payload_json`=4) in the framework's gRPC runtime
   (until the T8.6 split: `src/common/Ark.Tools.MediatorFramework`).
2. Extend the server interceptor: catch `BusinessRuleViolationException`, build
   `Google.Rpc.Status` (`code = FailedPrecondition`, `message = Title`), pack
   the detail as `Any`, serialize the violation with
   `ArkSerializerOptions.JsonOptions` into `payload_json`, throw
   `RpcException` with the status in trailers (same mechanism as the existing
   `BadRequest` mapping).
3. Test with the generated proto client (`…Sample.GrpcClient`): invoke the
   violating handler over gRPC, unpack the detail, assert `type`/`title` and
   deserialize `payload_json` back to the violation.

### Step 6.4 — gRPC client-streaming upload (T8.4)

**Status: complete.** The shared runtime now provides metadata-first upload
contracts and `StreamingArkAttachment`, the sample hosts the `Documents.Upload`
client-streaming service through the existing `UploadGreetingHandler`, and the
generated client tests cover a two-chunk payload plus missing metadata.

1. Add the chunked upload contracts (`UploadDocumentChunk` with `oneof`
   metadata/data, `UploadDocumentMetadata`) per `design.md` §"Attachments and
   streaming"; chunk size 64KiB.
2. Implement a stream adapter in the framework's gRPC runtime that exposes the
   incoming `IAsyncEnumerable<UploadDocumentChunk>` as an `IArkAttachment`
   whose `OpenRead()` returns a forward-only, non-seekable `Stream` (throw
   `InvalidOperationException` if the first message is not metadata).
3. Host a client-streaming method on the sample service `partial` invoking the
   *same* pure attachment handler used by the HTTP multipart endpoint.
4. Test: stream >64KiB (at least two data chunks) and assert the handler
   received the full content; assert a stream missing the metadata-first
   message fails with `InvalidArgument`.

### Step 6.5 — Version lifetime + `{version}` placeholder (T8.5)

Design first (already specified in `design.md` §"API versioning"), then:

1. Extend `TransportAttributes.cs`: add `int IntroducedIn` (default 1) and
   `int RetiredIn` (default 0 = never, exclusive when set) to `HttpEndpointAttribute`
   and `GrpcMethodAttribute`; change sample routes to the single-placeholder
   form `/api/v{version}/…` (remove hard-coded `v1`/`v2` routes).
2. Generator (`ArkEndpointGenerator.cs`): compute the hosted version set as
   `1..max(IntroducedIn, RetiredIn-1)` across all contracts; for each contract
   and each active version, emit the route with `{version}` substituted and
   tag the endpoint with that version for OpenAPI partitioning (replace the
   current route-parsing inference). For gRPC, emit one service per
   `[ServiceGroup]` per active version, suffixed `V{n}`, containing only the
   methods active in `n`.
3. Sample: keep the v1→v2 evolution (one contract retired in 2, its
   replacement introduced in 2) and at least one route-parameter (`{id}`)
   contract active in both versions.
4. Tests: same contract callable on `/api/v1/…` and `/api/v2/…`; retired
   contract 404s on v2; `{id}` binding works on both versions; OpenAPI v1/v2
   documents contain exactly the active endpoints; gRPC `GreetingsV1`/`…V2`
   services exist with the correct method sets. Update the migration guide's
   versioning wording (`migration-from-mvc.md`) — done in this change — and
   the generator snapshot tests.

### Step 6.6 — Package split per transport (T8.6)

1. Create `src/common/Ark.Tools.MediatorFramework.MinimalApi`,
   `…MediatorFramework.Rebus`, `…MediatorFramework.Grpc` (runtime) and a
   sibling `netstandard2.0` generator project for each
   (`…MinimalApi.Generators` etc., modeled on the existing analyzer packaging); the
   transport attribute moves into its transport runtime package;
   `Ark.Tools.MediatorFramework` keeps only `IArkAttachment`/`ArkAttachment`
   and shared versioning primitives.
2. Split `ArkEndpointGenerator.cs` by transport: each generator's syntax
   provider triggers **only** on its own attribute; shared emission helpers go
   to a `.Shared` source-only include (linked files, no runtime package).
3. Pack each generator into its runtime package's `analyzers/dotnet/cs` (or as
   its own package referenced with `PrivateAssets=all` — pick one, apply to all
   three consistently).
4. Update sample project references to the per-transport packages; delete the
   monolithic generator project; add `packages.lock.json` for every new
   project; `dotnet restore Ark.Tools.slnx --force-evaluate` then full build +
   tests.

### Step 6.7 — Framework capability tests (T8.7)

1. Create `tests/Ark.Tools.MediatorFramework.Tests` (auto-wired MSTest via
   `Directory.Build.props`; `IsPackable=false`).
2. Move/extend generator snapshot tests there (per-transport: endpoint
   emission, opt-in behavior, version expansion) plus runtime unit tests for
   the gRPC upload stream adapter and the `ArkBusinessRuleViolation` mapping —
   using in-memory compilations, no sample dependency.
3. Keep `…Sample.Tests` as the end-to-end "how to test an application built on
   the framework" demonstration; its README section should say exactly that.



- **Phases 1–4** are implemented and self-tested in the sample: pure handlers,
  generated Minimal API/Rebus/code-first gRPC dispatch, identity propagation,
  RFC 7807 responses, Rebus dead letters, OpenAPI versioning, JSON
  polymorphism, attachments and NodaTime protobuf support.
- The latest increment also proves an explicit MVC compatibility escape hatch:
  a hand-written controller negotiates MessagePack without changing the pure
  handler or generated endpoints. This is deliberately not counted as a new
  source-generated transport.
- **Phase 5** is complete: package validation/SBOM coverage runs in CI and the
  MVC migration guide is documented. The runtime and generator now live in
  `src/common` packages.
- **Phase 6** (review revisions) is specified above with per-step instructions
  and tracked as Epic 8 in [`tasks.md`](tasks.md); T8.1–T8.3 are implemented
  and self-tested, and implementation continues with T8.4.

The first build attempt on a fresh checkout failed because `--no-restore` was
used before assets existed. The verified sequence is `dotnet restore
Ark.Tools.slnx` (use `--force-evaluate` when central package versions changed),
then `dotnet build Ark.Tools.slnx --configuration Debug --no-restore`.
