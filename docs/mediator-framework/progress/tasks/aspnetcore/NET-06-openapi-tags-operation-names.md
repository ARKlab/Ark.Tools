# NET-06 — OpenAPI tags and operation names from the contract

**Category**: aspnetcore · **Priority**: **Release blocker** · **Scope**: FRAMEWORK + SAMPLE

## Problem

Generated operations carry no tag and no operation name. `MinimalApiEndpointGenerator` emits
`.Produces<…>()` / `.WithGroupName("v1")` only
(`src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi.Generators/MinimalApiEndpointGenerator.cs`,
around the `Produces` calls). Consequences:

- every operation lands in the default tag of the document, so Scalar/Swagger UI show one flat list;
- `operationId` is either missing or an unreadable compiler-generated name, so generated clients
  (NSwag/openapi-generator) produce meaningless method names;
- gRPC service grouping (`[GrpcService]`) and HTTP grouping are unrelated, so the two transports
  present different taxonomies for the same contracts.

## Design

See `docs/mediator-framework/design.md` → *OpenAPI operation grouping, naming and documentation*.

- **Tag** default = last segment of the contract type's namespace
  (`…Application.Greetings.GetGreetingQuery` → `Greetings`).
- **Operation name** default = contract type name (`GetGreetingQuery`), used for both
  `WithName(...)` (endpoint name) and the OpenAPI `operationId`.
- Versioned expansion must keep `operationId` unique per document: append the version suffix
  (`GetGreetingQuery_v2`) **only** when the same contract is expanded into more than one version
  document.
- Override: new `[ApiGroup("Greetings")]` attribute (class-level, `AttributeTargets.Class`), placed in
  the transport-neutral core package `Ark.Tools.MediatorFramework` so Application assemblies do not
  reference ASP.NET Core (same rule as `[BindFromQuery]`/`[ServerSet]`).
- The gRPC generator uses the same `[ApiGroup]` value as the service-group fallback when
  `[GrpcService]` is absent, so both transports group identically. An explicit `[GrpcService]`
  still wins for gRPC.

## Steps

1. Add `ApiGroupAttribute` (sealed, `[AttributeUsage(AttributeTargets.Class, AllowMultiple = false,
   Inherited = false)]`, single `string Name` ctor property) to
   `src/mediator-framework/Ark.Tools.MediatorFramework/`, with XML docs and the standard copyright
   header.
2. In `MinimalApiEndpointGenerator`, compute `tag` and `operationName` per endpoint during semantic
   analysis (they belong on the endpoint model record, not in the emitter), and emit
   `.WithTags("<tag>").WithName("<operationName>")` on every mapped endpoint, including the
   multipart, download and command paths. Escape the literals.
   Docs: [`WithTags`](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi#openapi-operation-tags),
   [`WithName` / operationId](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/openapi#operationid).
3. In `GrpcEndpointGenerator`, fall back to `[ApiGroup]` before the namespace default when grouping
   methods into a service.
4. Report a generator diagnostic (next free `ARKMF0xx`, documented in `design.md`) when two contracts
   in the same document/version resolve to the same operation name — a duplicate `operationId` breaks
   client generation and must not be emitted silently.
5. Add `[ApiGroup]` to at least one sample contract and rely on the namespace default for the others, so
   both paths are demonstrated in `samples/Ark.MediatorFramework.Sample`.

## Test coverage (required)

- `tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs`: snapshot shows `.WithTags`
  and `.WithName` for a default-tag contract, an `[ApiGroup]`-overridden contract and a versioned
  contract (unique `operationId` per version).
- New generator test asserting the duplicate-operation-name diagnostic is reported.
- Sample test that fetches `/openapi/v1.json` and asserts: every operation has a non-empty `tags[0]`
  and a unique `operationId`; the `[ApiGroup]` override is honoured; the namespace default is applied
  elsewhere.
- gRPC: an existing exported-proto assertion is extended to prove the `[ApiGroup]` fallback groups the
  method into the expected service.

## Outcomes

- Every generated HTTP operation carries a deterministic tag and a stable, unique `operationId`
  derived from the contract, overridable with `[ApiGroup]`; gRPC grouping uses the same taxonomy.

## Acceptance

- [x] `ApiGroupAttribute` exists in `Ark.Tools.MediatorFramework` with XML docs.
- [x] Generated endpoints emit `.WithTags(...)` and `.WithName(...)`; snapshots updated.
- [x] `operationId` is unique within each versioned document (tested).
- [x] Duplicate operation names produce a documented generator diagnostic (tested).
- [x] gRPC service grouping falls back to `[ApiGroup]`; explicit `[GrpcService]` still wins (tested).
- [x] `design.md` documents the defaults, the override and the new diagnostic id.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
