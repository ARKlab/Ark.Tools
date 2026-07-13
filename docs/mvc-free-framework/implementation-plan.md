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

## Phase 7 — Second review revisions (step-by-step, executable)

Same general rules as Phase 6: execute in order; build with `dotnet build
Ark.Tools.slnx --configuration Debug`; test the affected project with
`dotnet test <project> -f net10.0`; on any `PackageReference` change add the
version to `Directory.Packages.props` and run `dotnet restore Ark.Tools.slnx
--force-evaluate` to refresh lock files; one Conventional Commit per step.

### Step 7.1 — gRPC error interceptor into the library (T9.1)

1. Move `GrpcErrorInterceptor.cs` from
   `samples/…/Ark.MediatorFramework.Sample.WebInterface` to
   `src/common/Ark.Tools.MediatorFramework.Grpc/ArkGrpcErrorInterceptor.cs`
   (rename class to `ArkGrpcErrorInterceptor`, namespace
   `Ark.Tools.MediatorFramework.Grpc`, XML docs on all public members). Add the
   needed `Google.Api.CommonProtos`/`Grpc.StatusProto`/`FluentValidation`
   references to the Grpc runtime csproj (already centrally versioned).
2. Reshape `ArkBusinessRuleViolation`
   (`src/common/Ark.Tools.MediatorFramework/ArkBusinessRuleViolation.cs`, move
   it into the Grpc package with the interceptor): fields `type`=1, `title`=2,
   `status`=3, `detail`=4 (string, optional), `instance`=5 (string, optional),
   `extensions`=6 `map<string,string>`. Remove `payload_json`.
3. Interceptor mapping: `detail` ← `BusinessRuleViolation.Detail`-equivalent
   (empty when absent), `instance` ← empty (reserved), `extensions` ← one entry
   per extra public property of the derived violation, each **value**
   serialized with `ArkSerializerOptions.JsonOptions`
   (`JsonSerializer.Serialize(value, property.PropertyType, options)`).
4. Sample: delete the local interceptor, register
   `ArkGrpcErrorInterceptor` from the library in `SampleStartup`.
5. Tests: unit tests in `tests/Ark.Tools.MediatorFramework.Tests` for both
   exception mappings and the map encoding; update
   `…Sample.Tests` gRPC violation test + the emitted proto snapshot to the new
   fields.

### Step 7.2 — Rebus.Protobuf (T9.2)

1. Add `Rebus.Protobuf` to `Directory.Packages.props` and to the sample
   WebInterface csproj (run the advisory check first).
2. Replace `ProtobufRebusSerializer` usage in the Rebus configuration with
   `.Serialization(s => s.UseProtobuf(model))` where `model =
   RuntimeTypeModel.Create().AddNodaTimeSurrogates()`; delete
   `ProtobufRebusSerializer.cs`.
3. Run the existing protobuf-over-Rebus test; refresh lock files.

### Step 7.3 — MinimalApi hosting helpers (T9.3)

1. In `src/common/Ark.Tools.MediatorFramework.MinimalApi` add:
   - `ArkOpenApiEx.AddArkNodaTimeSchemas(this OpenApiOptions)` — port the
     NodaTime branch of `SampleStartup.ConfigureOpenApi` 1:1;
   - `ArkOpenApiEx.AddArkPolymorphism<TBase, TDiscriminator>(this
     OpenApiOptions, string discriminatorProperty,
     params (TDiscriminator Value, Type DerivedType)[] mapping)` — port the
     `Shape` branch, generalized (component registration, `oneOf`,
     `discriminator` mapping);
   - `ArkMultipartEx.MapArkAttachmentUpload<TRequest, TResponse>(this
     IEndpointRouteBuilder, string pattern, Func<IArkAttachment, TRequest>
     factory)` — port `_uploadGreetingCard` (read form, map `IFormFile` →
     `ArkAttachment`, resolve `IRequestHandler<TRequest,TResponse>` from the
     `Container`, return `TypedResults.Ok`).
2. Rewrite `SampleStartup` to consume the three helpers; delete the ported
   private code.
3. Unit tests in `tests/Ark.Tools.MediatorFramework.Tests` (schema transformer
   output via `OpenApiOptions` harness; multipart mapping via `TestServer`);
   sample tests must stay green unchanged.

### Step 7.4 — Cross-transport polymorphism (T9.4)

**Status: complete.** The sample `Shape` hierarchy now uses matching
`ProtoInclude`/MessagePack union keys alongside the existing JSON discriminator,
is exposed by the generated gRPC service and the MessagePack controller, and has
a three-wire parity test.

1. Annotate `Shape`/`Circle`/`Square`
   (`…Sample.Application/PolymorphicContracts.cs`) with
   `[ProtoContract]` + `[ProtoInclude(10, typeof(Circle))]` /
   `[ProtoInclude(11, typeof(Square))]` and `[MessagePack.Union(10,
   typeof(Circle))]` / `[Union(11, typeof(Square))]` (same numbers; keep the
   STJ converter). Add `MessagePack` reference to Application if missing.
2. Expose `DescribeShapeRequest` over gRPC (`[GrpcMethod]`, `[ServiceGroup]`)
   and over MessagePack (reuse Step 7.5's serde or the MVC controller until
   then).
3. Tests: gRPC round-trip using the proto-generated client (`oneof`-style
   subtype fields), MessagePack round-trip, and a three-wire parity assertion;
   comment in the contract file records the JSON-stays-discriminator decision.

### Step 7.5 — MessagePack serde on Minimal API (T9.5)

**Status: complete.** The Minimal API runtime now provides an opt-in
MessagePack-aware POST mapper used by the generated endpoint registration.
JSON remains supported, while MessagePack requests and responses use the
configured NodaTime resolver. The sample's behavioral tests exercise greeting
and polymorphic shape round-trips through the generated routes; the MVC
controllers remain compatibility escape-hatch examples.

1. In `Ark.Tools.MediatorFramework.MinimalApi` add an endpoint filter/helper
   (`ArkMessagePackEx`) that, when the request `Content-Type` is
   `application/x-msgpack`, deserializes the body with
   `MessagePackSerializer` (options with NodaTime resolver, as the MVC
   formatter does) and, when `Accept` prefers msgpack, serializes the response
   likewise; JSON behavior unchanged otherwise.
2. Apply it to the generated endpoints in the sample
   (`MapArkEndpoints().AddEndpointFilter(…)` or an opt-in extension) and add a
   msgpack round-trip self-test including NodaTime values; keep the MVC
   controller as the escape-hatch demo with a README note.

### Step 7.6 — Proto-on-build + shared proto assets (T9.6)

**Status: complete.** The gRPC generator emits per-service-group proto assets,
the transport packages ship shared proto content and build-transitive export
support, and the sample client compiles against the exported files.

1. Grpc generator (`GrpcEndpointGenerator.cs`): from the discovered contracts
   also emit `ArkGeneratedProtos` — a static class with one
   `(string FileName, string Content)` entry per `[ServiceGroup]`; each file
   starts with `syntax = "proto3";`, `import "ark/nodatime.proto";`,
   `import "ark/mediator.proto";` and contains only that group's messages and
   per-version services (reuse the emission logic currently in
   `samples/…/Ark.MediatorFramework.ProtoGenerator/Program.cs`, including the
   NodaTime/common-type name mapping, minus the shared messages).
2. Author the shared files as package content:
   `src/common/Ark.Tools.Nodatime.Protobuf/proto/ark/nodatime.proto`
   (surrogate messages) packed with
   `Pack="true" PackagePath="content/proto/ark;contentFiles/any/any/proto/ark"`;
   `src/common/Ark.Tools.MediatorFramework.Grpc/proto/ark/mediator.proto`
   (`ArkBusinessRuleViolation` with the Step 7.1 shape, `UploadDocumentChunk`,
   `UploadDocumentMetadata`) packed the same way.
3. Runtime export: add `ArkProtoExport.TryHandle(string[] args)` to the Grpc
   runtime (writes `ArkGeneratedProtos` + copies of the shared files into the
   `--ark-export-proto <dir>` argument, returns `true` when it handled the
   invocation); call it first thing in the sample `Program`.
4. Ship `buildTransitive/Ark.Tools.MediatorFramework.Grpc.targets` with target
   `ArkExportProto` (`AfterTargets="Build"`,
   `Condition="'$(ArkExportProtoDir)' != ''"`) executing
   `dotnet "$(TargetPath)" --ark-export-proto "$(ArkExportProtoDir)"`; wire the
   sample WebInterface with
   `<ArkExportProtoDir>$(MSBuildProjectDirectory)/proto</ArkExportProtoDir>`.
5. Point `…Sample.GrpcClient`'s `<Protobuf>` items at the exported directory;
   delete `samples/…/Ark.MediatorFramework.ProtoGenerator` (project + slnx
   entry + any invoking target); add a proto snapshot test in
   `tests/Ark.Tools.MediatorFramework.Tests`; full build + tests + lock files.

### Step 7.7 — HTTP→Rebus composition (T9.7)

1. Add `GreetingFollowUpRequested` (`[RebusMessage]`) to the Application; make
   the HTTP-exposed create-greeting handler send it via an injected `IBus`
   after the synchronous work; its Rebus handler records the follow-up in the
   store (queryable via an existing/new query endpoint).
2. Test: POST over HTTP, then poll the query endpoint (bounded retry) until
   the async effect appears.

### Step 7.8 — Reqnroll behavioral tests (T9.8)

1. Add `Reqnroll.MsTest` + `Reqnroll.Tools.MsBuild.Generation` (already
   centrally versioned) to `…Sample.Tests`; create `Features/` with Gherkin
   scenarios: greeting create+query (HTTP), create via gRPC + query via HTTP,
   business-rule violation (400 problem+json), versioning (v1 vs v2), HTTP→Rebus
   composition.
2. Step definitions use only the `TestServer` HTTP client and the generated
   gRPC client — no direct container/handler/store access; share host setup
   with the existing fixture. Keep plain MSTest classes for transport plumbing
   assertions.
3. Update the sample README testing section.

### Step 7.9 — Refinement sweep (T9.9)

1. Re-read `design.md` end-to-end and diff against the implementation; move any
   remaining reusable piece out of the sample into its `src/` package with unit
   tests in `tests/Ark.Tools.MediatorFramework.Tests`.
2. Verify: `WebInterface` = wiring + escape hatches only; every `src/` feature
   unit-tested; sample behavior tests interface-only; protos generated on
   build and consumed by the client project. Update docs where reality
   diverged.

## Phase 8 — Third review revisions (step-by-step, executable)

Same general rules as Phases 6–7, plus one hard gate added by this review:
**every step ends with a full-solution build** — `dotnet build Ark.Tools.slnx
--configuration Debug` must succeed (all projects, not only the touched ones)
before the step's commit. Execute in the order of `tasks.md` ("Next
implementation order").

### Step 8.1 — NodaTime `google.type.DateTime` mappings (T10.1)

**Status: complete.** `LocalDateTime`, `OffsetDateTime`, and `ZonedDateTime`
now use the `google.type.DateTime` field layout; offset and zone data use the
matching nested Google message shapes. Round-trip coverage includes the new
zoned type and the existing native mappings.

1. In `src/common/Ark.Tools.Nodatime.Protobuf/Surrogates/` replace
   `LocalDateTimeSurrogate` and `OffsetDateTimeSurrogate` and add
   `ZonedDateTimeSurrogate`, all three mirroring
   `google.type.DateTime` exactly: `[ProtoContract(Name = "DateTime")]`
   fields `year`=1, `month`=2, `day`=3, `hours`=4, `minutes`=5, `seconds`=6,
   `nanos`=7 (all int32) plus the `time_offset` oneof — `utc_offset`=8
   (Duration-shaped sub-message: `seconds` int64=1, `nanos` int32=2) and
   `time_zone`=9 (TimeZone-shaped sub-message: `id` string=1, `version`
   string=2). protobuf-net has no oneof on surrogates: model it as two
   optional fields where exactly zero or one is populated.
   - `LocalDateTime` ↔ neither field set;
   - `OffsetDateTime` ↔ `utc_offset` set (offset → Duration seconds/nanos);
   - `ZonedDateTime` ↔ `time_zone` set (`id` = `Zone.Id`), converting via
     `LocalDateTime` + zone (use `DateTimeZoneProviders.Tzdb`; unknown id on
     decode → `DateTimeZoneNotFoundException`).
2. Register the `ZonedDateTime` surrogate in `Ex.AddNodaTimeSurrogates`.
3. Update `tests/Ark.Tools.Nodatime.Protobuf.Tests`: round-trips for all
   three (`LocalDateTime` zoneless, `OffsetDateTime` offset preserved,
   `ZonedDateTime` zone id preserved), schema/wire-shape assertions against
   the google.type field numbers, and keep the existing native-mapping tests.
4. `dotnet test tests/Ark.Tools.Nodatime.Protobuf.Tests -f net10.0`; **full
   solution build**.

### Step 8.2 — Shared proto namespaces (T10.2)

1. `src/common/Ark.Tools.Nodatime.Protobuf/proto/ark/nodatime.proto`: add
   `package ark.nodatime;`, set
   `option csharp_namespace = "Ark.Tools.Nodatime.Protobuf";`, add
   `import "google/type/datetime.proto";` and delete the `LocalDate`,
   `LocalDateTime` and `OffsetDateTime` messages — only `Period` remains
   (date/time members in generated files reference `google.type.Date` /
   `google.type.DateTime` directly).
2. `src/common/Ark.Tools.MediatorFramework.Grpc/proto/ark/mediator.proto`:
   add `package ark.mediator;`, set
   `option csharp_namespace = "Ark.Tools.MediatorFramework.Grpc";`.
3. Update the Grpc generator's proto emission (type-name mapping now emits
   `google.type.DateTime`/`google.type.Date` and the `ark.nodatime.Period` /
   `ark.mediator.*` qualified names) and the proto snapshot test in
   `tests/Ark.Tools.MediatorFramework.Tests`.
4. `…Sample.GrpcClient`: ensure `google/type/*.proto` are importable
   (`Grpc.Tools` ships the well-known types; add
   `Google.Api.CommonProtos`-provided protos to `<Protobuf>` includes or
   `AdditionalImportDirs` as needed); regenerate; fix the client-side
   namespaces in `…Sample.Tests`.
5. All gRPC tests + snapshot green; **full solution build**.

### Step 8.3 — MinimalApi net10.0 only (T10.6)

**Status: complete.** `Ark.Tools.MediatorFramework.MinimalApi` now targets only
`net10.0`; its OpenAPI reference and source set no longer contain net8-specific
conditions. The refreshed lock files and full solution validation are green.

1. `src/common/Ark.Tools.MediatorFramework.MinimalApi/…csproj`: single
   `<TargetFramework>net10.0</TargetFramework>`; delete every
   `Condition="'$(TargetFramework)' == 'net8.0'"` group and net8-only
   package references.
2. `dotnet restore Ark.Tools.slnx --force-evaluate` (lock files shrink);
   **full solution build** + affected tests.

### Step 8.4 — HTTP envelope binding (T10.3)

**Status: complete.** Generated body endpoints now bind route and opted-in query
properties separately, then overwrite those values on the deserialized request
envelope before dispatch. The sample and generator tests cover the combined
route/query/body case.

1. `src/common/Ark.Tools.MediatorFramework/`: add
   `BindFromQueryAttribute` (property-level, transport-metadata only, XML
   docs) next to the existing transport attributes.
2. `MinimalApiEndpointGenerator`: per contract compute the binding plan —
   route properties = case-insensitive match with template placeholders
   (excluding `{version}`); query properties = `[BindFromQuery]` (body verbs)
   or all remaining (GET/DELETE); body = the envelope itself for body verbs.
   Emit a lambda taking `[FromRoute]`/`[FromQuery]` parameters plus (for body
   verbs) the deserialized envelope, then rebuild the envelope with an object
   initializer/`with` expression overwriting the bound members before
   dispatch. GET/DELETE keep `[AsParameters]` only when no explicit plan is
   needed.
3. Sample: extend one contract (e.g. `UpdateGreetingRequest`) to combine a
   `{id}` route member, a `[BindFromQuery]` member and body members; add
   generator snapshot coverage in `tests/Ark.Tools.MediatorFramework.Tests`
   and a behavioral test asserting all three sources arrive in the handler.
4. **Full solution build** + tests.

### Step 8.5 — Generated multipart upload (T10.4)

1. `MinimalApiEndpointGenerator`: when a `[HttpEndpoint]` contract has exactly
   one `IArkAttachment`-typed property, emit a multipart endpoint instead of
   the JSON body binding: `Accepts("multipart/form-data")`, read
   `httpContext.Request.Form.Files` (require exactly one file), map to
   `new ArkAttachment(...)`, bind route/query members per Step 8.4, assign the
   attachment property, dispatch. Two or more attachment properties → report
   a generator error diagnostic (`ARKMF00x`).
2. Sample: put `[HttpEndpoint]` (with a route/query member) on
   `UploadGreetingCardRequest`; delete the `MapArkAttachmentUpload` call from
   `SampleStartup` (keep the helper + one escape-hatch mention in README).
3. Tests: generator snapshot + diagnostic test; upload behavioral test now
   hits the generated route and asserts the bound route/query member.
4. **Full solution build** + tests.

### Step 8.6 — MessagePack content negotiation (T10.5)

1. `HttpEndpointAttribute`: add a serialization opt-in (e.g.
   `bool AcceptsMessagePack` or a `[Flags] HttpSerde` property, JSON always
   on). Remove the `useMessagePack` parameter from the generated
   `MapArkEndpoints`.
2. Replace `ArkMessagePackEx.MapArkMessagePackPost` with an endpoint filter /
   inline negotiation used by the generated endpoint when the contract opted
   in: `Content-Type: application/x-msgpack` → deserialize body with
   `MessagePackSerializer`; `Accept` preferring msgpack → serialize response
   likewise; JSON otherwise. Delete `MapArkMessagePackPost`.
3. `GetOptions`: resolve `IFormatterResolver` with
   `GetRequiredService<IFormatterResolver>()` — **delete the
   `CompositeResolver` fallback**; sample registers its resolver in DI.
4. Tests: msgpack round-trip, mixed-negotiation (json-in/msgpack-out and
   inverse), and missing-resolver → exception. Update generator snapshots.
5. **Full solution build** + tests.

### Step 8.7 — OpenAPI NodaTime parity (T10.7)

1. Extend `ArkOpenApiEx.AddArkNodaTimeSchemas` to cover `Instant`,
   `ZonedDateTime`, `LocalTime`, `DateTimeZone` (and keep `LocalDate`,
   `LocalDateTime`, `OffsetDateTime`, `Period`), mirroring
   `SupportNodaTimeExtensions`' formats/examples; nullable forms follow the
   underlying type automatically in STJ-based OpenAPI — assert they do.
2. Unit test: one schema assertion per type in
   `tests/Ark.Tools.MediatorFramework.Tests`.
3. **Full solution build** + tests.

### Step 8.8 — Package-shaped consumption (T10.8)

1. Grpc MSBuild assets: move the `ArkExportProtoDir` opt-in documentation and
   the export/copy wiring fully into
   `src/common/Ark.Tools.MediatorFramework.Grpc/buildTransitive/` (`.props`
   for defaults + existing `.targets`), packed via
   `<None Pack="true" PackagePath="buildTransitive" />`. Verify a packed
   `dotnet pack` layout contains them; if csproj packing can't express the
   layout, create `Ark.Tools.MediatorFramework.Grpc.MsBuild`
   (nuspec-authored) and make the Grpc package depend on it.
2. Move the transport stack dependencies into the framework csprojs with
   default (transitive) assets: `protobuf-net.Grpc.AspNetCore`,
   `Grpc.StatusProto`, `System.ServiceModel.Primitives` → Grpc;
   `Rebus.Protobuf` → Rebus; `Hellang.Middleware.ProblemDetails`,
   `MessagePack`, `Microsoft.AspNetCore.OpenApi` → MinimalApi. Delete them
   from the sample WebInterface csproj.
3. Analyzer flow: make each runtime package carry its generator as an
   analyzer asset (`analyzers/dotnet/cs` on pack; for the in-repo
   project-resolution path add a `buildTransitive` `.props` adding the
   generator dll as an `Analyzer` item) so consumers need one reference per
   transport.
4. Sample references via the ReferenceProject trick: add
   `Ark.Tools.MediatorFramework*`, `Ark.Tools.Rebus`,
   `Ark.Tools.AspNetCore.MessagePack` etc. as `PackageVersion 999.9.9`
   in a sample-level `Directory.Packages.props` (or the root one) and switch
   the sample csprojs from `ProjectReference` to `PackageReference`; confirm
   the lock file records them as `"type": "Project"`.
5. Delete the sample-specific `proto/*.proto` line from the root `.gitignore`;
   add a local `samples/…/WebInterface/proto/.gitignore` (or ignore `proto/`
   in the sample folder) instead. Remove the manual `Import` +
   `CopyExportedProto` target from the sample csproj.
6. `dotnet restore Ark.Tools.slnx --force-evaluate`; **full solution build** +
   all tests; lock files committed.

### Step 8.9 — Design-conformance sweep (T10.9, merged with T9.9)

1. Diff the PR against `design.md` section by section; fix drift or amend the
   design with rationale.
2. Confirm the always-build gate ran for every step (CI green on
   locked-mode restore, build, tests).



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
- **Phase 6** (review revisions) is complete: T8.1–T8.7 are implemented and
  self-tested, tracked as Epic 8 in [`tasks.md`](tasks.md).
- **Phase 7** (second review revisions) is tracked as Epic 9 in
  [`tasks.md`](tasks.md); Steps 7.1–7.6 are complete, Steps 7.7–7.9 remain.
- **Phase 8** (third review revisions) is specified above with per-step
  instructions and tracked as Epic 10 in [`tasks.md`](tasks.md); execution
  follows the "Next implementation order" in `tasks.md` (Phase 8 wire-shape
  and packaging steps before the remaining Phase 7 behavioral steps), with the
  full-solution build gate on every step.

The first build attempt on a fresh checkout failed because `--no-restore` was
used before assets existed. The verified sequence is `dotnet restore
Ark.Tools.slnx` (use `--force-evaluate` when central package versions changed),
then `dotnet build Ark.Tools.slnx --configuration Debug --no-restore`.
