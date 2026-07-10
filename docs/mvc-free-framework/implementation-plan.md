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

Already available in the repo: `SimpleInjector`, `Rebus`, `Ark.Tools.Solid*`,
`Ark.Tools.SimpleInjector`, `Ark.Tools.Rebus`, MSTest + Microsoft.Testing.Platform.

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

- Extract the runtime support + generator into `src/` packages
  (`Ark.Tools.MediatorFramework`, `Ark.Tools.MediatorFramework.Generators`).
- Add `packages.lock.json` for all new projects (CI runs `RestoreLockedMode`).
- Package validation, SBOM, XML docs on all public APIs.
- Migration guidance for existing MVC controllers.

## Delivery status in this PR

- **Phases 1–4** are implemented and self-tested in the sample: pure handlers,
  generated Minimal API/Rebus/code-first gRPC dispatch, identity propagation,
  RFC 7807 responses, Rebus dead letters, OpenAPI versioning, JSON
  polymorphism, attachments and NodaTime protobuf support.
- The latest increment also proves an explicit MVC compatibility escape hatch:
  a hand-written controller negotiates MessagePack without changing the pure
  handler or generated endpoints. This is deliberately not counted as a new
  source-generated transport.
- **Phase 5** remains open: extract the runtime/generator into `src/` packages,
  add package validation/SBOM coverage, and write the MVC migration guide.

The first build attempt on a fresh checkout failed because `--no-restore` was
used before assets existed. The verified sequence is `dotnet restore
Ark.Tools.slnx` (use `--force-evaluate` when central package versions changed),
then `dotnet build Ark.Tools.slnx --configuration Debug --no-restore`.
