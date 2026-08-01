# Azure Functions hosting — decision log

Status: **review required**. AZD-01 through AZD-09 were accepted on 2026-07-31.
AZD-10 and AZD-11 were added after implementation-mechanism review and must be
accepted before their blocked tasks begin. Accepted decisions are reflected in
[`../azure-functions-design.md`](../azure-functions-design.md) and the affected
tasks.

## How to review

- Approve the recommended choice, select an alternative, or add a constraint.
- Do not begin a task whose `Blocked tasks` include an unresolved decision.
- A decision records observable behavior, not only an implementation preference.

## Proposed decisions

### AZD-01 — Target only .NET 10 isolated worker

- **Status:** DECIDED
- **Decision:** target `net10.0`, matching the current Minimal API package
  and sample. Use Azure Functions runtime v4 with the Microsoft-documented minimum
  Worker/Worker.Sdk versions for .NET 10.
- **Alternative:** multi-target the Functions runtime package for `net8.0` and
  `net10.0`.
- **Why this needs review:** Ark.Tools generally supports both TFMs, but the
  existing Minimal API transport currently targets only `net10.0`; multi-targeting
  only the Function transport adds a parity and test matrix with no current host.
- **Blocked tasks:** AZF-01, AZF-02, AZF-08.

### AZD-02 — Compile-time host opt-in and version prefix

- **Status:** DECIDED
- **Decision:** require one shared assembly-level HTTP host marker with
  `VersionPrefix = "/api/v{version}"`. The Functions generator emits nothing
  without it. The Minimal API generator supports the same marker and prefix while
  preserving its existing mapping API for backward compatibility. AZD-10 refines
  how the marker selects contract assemblies and contracts.
- **Alternative:** expose MSBuild analyzer properties, or generate automatically
  from every runtime package reference.
- **Why this needs review:** Functions routes live in attributes and cannot receive
  the Minimal API mapping-time prefix. An assembly marker is typed, discoverable
  and explicit but becomes public API.
- **Blocked tasks:** AZF-01, AZF-03.

### AZD-03 — Trigger authorization level versus application authentication

- **Status:** DECIDED
- **Decision:** emit `AuthorizationLevel.Anonymous` for all generated
  triggers and enforce `AllowAnonymous` through ASP.NET Core authentication in the
  runtime helper. This preserves the Minimal API bearer-token contract. Document
  Function keys and Easy Auth as optional perimeter controls.
- **Alternative A:** map public endpoints to `Anonymous` and protected endpoints
  to `Function`, requiring a Function key instead of bearer parity.
- **Alternative B:** require Easy Auth for every deployment and trust only its
  injected identity.
- **Why this needs review:** Functions authorization levels are access keys, not
  ASP.NET Core policies. Combining both without a declared profile can create
  surprising double authentication or accidentally anonymous endpoints.
- **Blocked tasks:** AZF-04, AZF-05, AZF-08.

### AZD-04 — Supported authentication profiles

- **Status:** DECIDED
- **Decision:** ship a bearer profile using registered ASP.NET Core
  `IAuthenticationService`; treat Easy Auth identity reconstruction as a separately
  tested opt-in profile. Never trust `X-MS-CLIENT-PRINCIPAL` merely because a
  caller supplied the header. The first sample uses direct bearer authentication;
  Easy Auth coverage exercises the opt-in profile.
- **Alternative:** support Easy Auth only and leave direct bearer validation to
  API Management.
- **Blocked tasks:** AZF-05, AZF-08.

### AZD-05 — OpenAPI parity

- **Status:** DECIDED
- **Decision:** include generated OpenAPI metadata/document production in
  scope, with one document per API version and the same routes, schemas, standard
  responses, tags, operation names and security requirements. Do not use an
  extension that requires duplicating annotations on generated functions.
- **Alternative:** declare OpenAPI a known first-release gap and validate only the
  callable HTTP surface.
- **Why this needs review:** ASP.NET Core endpoint metadata does not exist in a
  Functions app. Full parity therefore needs generator-owned document metadata or
  a compatible adapter and is a distinct deliverable.
- **Blocked tasks:** AZF-07, AZF-09.

### AZD-06 — JSON streaming release gate

- **Status:** DECIDED
- **Decision:** require a Core Tools proof that the first JSON item reaches
  the client before the producer completes and that disconnect cancellation
  reaches the handler. If either fails, stop the streaming task and record Azure
  Functions streaming as unsupported rather than buffer silently.
- **Alternative:** exclude `IAsyncEnumerable<T>` endpoints from generated
  Functions with a diagnostic from the first release.
- **Why this needs review:** ASP.NET Core integration exposes request/response
  objects but not the complete ASP.NET Core server pipeline; platform buffering
  must be measured.
- **Blocked tasks:** AZF-07, AZF-08, AZF-09.

### AZD-07 — Functions boundary-test execution

- **Status:** DECIDED
- **Decision:** install/pin Azure Functions Core Tools in CI and run the complete
  dedicated boundary-test job on every pull request. Unit/helper tests remain in
  the normal solution test command; the boundary job may be selected by category
  but may not silently skip.
- **Alternative:** use a Functions runtime container in CI.
- **Blocked tasks:** AZF-08, AZF-09.

### AZD-08 — One-way Rebus transport in local sample tests

- **Status:** DECIDED
- **Decision:** promote the existing drainable in-memory one-way transport test
  helper into an appropriate reusable test surface without changing its existing
  behavior. Add a separately named `DrainableV2` only if the Functions scenario
  requires incompatible semantics. Azure configuration uses
  `UseAzureServiceBusAsOneWayClient`. The Function host never registers Rebus
  receivers.
- **Alternative:** use Azurite-equivalent infrastructure where possible (Azure
  Service Bus has no Azurite emulator), or restrict the demonstration to a mocked
  `IBus`.
- **Why this needs review:** a mock would not prove routing or one-way transport;
  the current drainable transport is internal repository test code and may need an
  intentional public/test package home.
- **Blocked tasks:** AZF-06, AZF-08.

### AZD-09 — Scope of “same endpoints”

- **Status:** DECIDED
- **Decision:** generate every host-selected sample `[HttpEndpoint]`. A selected
  contract with `AcceptsMessagePack = true` produces a compile-time error diagnostic
  in an Azure Functions host; a contract explicitly excluded by the host selection
  does not. gRPC-only, Rebus-only, controllers and handwritten escape hatches are
  not Function endpoints.
- **Alternative:** select a representative subset for the first sample.
- **Why this needs review:** endpoint-by-endpoint parity is achievable only if
  streaming and OpenAPI decisions pass. A subset demonstrates capabilities but
  does not satisfy literal same-surface parity.
- **Blocked tasks:** AZF-03 through AZF-09.

### AZD-10 — Host contract selection and composition

- **Status:** PROPOSED — review required
- **Recommendation:** allow one or more shared assembly-level HTTP host markers in
  each host assembly. Each marker selects a contract assembly through a marker
  `Type`, uses the host-wide `VersionPrefix`, and may provide exact
  `IncludedContracts` or `ExcludedContracts`; the two lists are mutually exclusive.
  Empty inclusion/exclusion lists select every `[HttpEndpoint]` in that contract
  assembly, preserving assembly-wide API surfaces. Multiple markers compose
  contracts from multiple assemblies; all markers in one host must agree on the
  version prefix. The Minimal API and Functions generators consume the same
  selection model, while the existing Minimal API mapping API remains backward
  compatible.
- **Diagnostic rule:** validate unknown, duplicate, cross-assembly and conflicting
  selections. Report unsupported-transport diagnostics, including MessagePack, only
  for contracts selected into the current host. This permits a Minimal API host to
  expose a MessagePack-enabled contract while a sibling Functions host explicitly
  excludes it.
- **Alternative A:** keep assembly-only selection. This forces a separate contract
  assembly whenever two hosts need different surfaces.
- **Alternative B:** put transport-specific exposure attributes on Application
  contracts. This couples contracts to hosts and is rejected.
- **Why this needs review:** assembly-only discovery is simple and valid for one API
  surface, but it cannot compose multiple contract assemblies or exclude one
  unsupported contract for a sibling host.
- **Blocked tasks:** AZF-01, AZF-02, AZF-08, AZF-09, AZF-10.

### AZD-11 — Function App OpenAPI production mechanism

- **Status:** PROPOSED — review required
- **Recommendation:** do not use
  `Microsoft.Azure.Functions.Worker.Extensions.OpenApi`. The Functions generator
  emits immutable host-selected operation/type descriptors from the shared HTTP
  semantic model. The Functions runtime package builds and caches one
  OpenAPI 3.1 `Microsoft.OpenApi` document per API version from those descriptors
  and the host's configured `JsonSerializerOptions`/`JsonTypeInfo`; generated
  anonymous HTTP triggers serve `/openapi/v{version}.json` and optionally YAML.
  Schema generation is host-neutral framework code shared with Ark OpenAPI
  conventions, not a reflection scan for endpoint contracts.
- **Rationale:** the official Azure Functions OpenAPI extension is in maintenance
  mode, supports OpenAPI only through 3.0.1, and expects OpenAPI attributes on
  Function methods. That would duplicate generator-owned contract metadata and
  diverge from the current Minimal API OpenAPI surface. `Microsoft.AspNetCore.OpenApi`
  cannot be used directly because Functions does not build an ASP.NET Core endpoint
  metadata graph; the already centrally pinned `Microsoft.OpenApi` object model and
  writers do not have that dependency.
- **Alternative A:** generate extension attributes on every Function and use the
  maintenance-mode extension.
- **Alternative B:** generate static JSON at compilation time. This cannot reliably
  honor host-configured JSON metadata, converters and polymorphism.
- **Alternative C:** omit OpenAPI from the first release, contradicting accepted
  AZD-05.
- **Blocked tasks:** AZF-01, AZF-09, AZF-10.

## Confirmed constraints from the request

| ID | Constraint |
| --- | --- |
| AZC-01 | Design and specification only in this PR; no production or sample code changes |
| AZC-02 | Use C# isolated worker with ASP.NET Core HTTP request/response integration |
| AZC-03 | New generator plus runtime/helper library |
| AZC-04 | Demonstrate a Function host inside the existing mediator sample |
| AZC-05 | Rebus is outbound-only; the Function app does not host a processor |
| AZC-06 | Demonstrate authorization, validation, ProblemDetails, uploads, downloads and testing |
| AZC-07 | MessagePack support with content negotiation is out of scope |
