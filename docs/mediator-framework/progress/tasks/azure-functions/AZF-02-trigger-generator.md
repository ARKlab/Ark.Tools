# AZF-02 — HTTP trigger generation, routing and version expansion

**Category**: azure-functions · **Priority**: core · **Scope**: GENERATOR

## Problem

Azure Functions discovers HTTP endpoints only from `[Function]` and
`[HttpTrigger]` metadata. Minimal API route mapping cannot execute in the isolated
Functions host, so every active mediator HTTP route needs deterministic trigger
source.

## Prerequisites

- AZF-01 merged.
- AZD-02, AZD-03, AZD-09 and AZD-10 decided.
- Use the exact Worker attribute APIs selected by AZF-01; do not infer signatures
  from memory.

## Implementation steps

1. Extend the incremental generator pipeline from the shared model to emit one
   public generated Function method per concrete route/version.
2. Emit the approved `AuthorizationLevel` explicitly, the single normalized HTTP
   method, and a trigger route without a leading slash.
3. Apply the configured prefix only to contracts that rely on host prefixing.
   Preserve explicit templates containing `{version}`, matching Minimal API rules.
4. Expand `Introduced` and exclusive `Retired` exactly once per active version.
   Never emit a retired route or bind `{version}` to a contract property.
5. Define stable Function names from API group, contract identity and version.
   Sanitize invalid characters and diagnose collisions before emission.
6. Emit a thin async method accepting `HttpRequest` and cancellation, then call a
   typed runtime dispatch entry point. Always `await` the dispatch call.
7. Preserve command/request/query handler type selection at compile time; generated
   source must not use reflection-based mediator dispatch.
8. Report diagnostics at the source contract/host marker for invalid verbs,
   templates, versions, duplicate routes, duplicate Function names and unsupported
   handler kinds. Report a compile-time error for `AcceptsMessagePack = true` only
   when that contract is selected into the current Functions host. Invalid selected
   endpoints emit no partial source; excluded contracts emit neither source nor
   unsupported-transport diagnostics.
9. Do not add generated Functions to API-surface snapshots. Generated Functions are
   host wiring, not contract surface: `[HttpEndpoint]` is the only HTTP contract
   surface and snapshots already record it via `[http=...]` routes. Host inventory
   (Function name, verb, route, version) is guarded by reflection-based route guard
   tests in `tests/` instead.

## Caveats

- ASP.NET Core integration supplies `HttpRequest`; do not emit `HttpRequestData`.
- Do not emit endpoint routing, route groups, middleware calls, Minimal API result
  types or `MapArkEndpointsFromAssembly`.
- Azure Functions `host.json` route prefix is host-wide. Generated contract routes
  must not rely on per-function host-prefix variation.
- Function discovery metadata must consist of compile-time constants.
- MessagePack is unsupported; do not emit a Function for a contract that opts into
  it.

## Required test coverage

- Explicit `/api/v{version}` and host-prefixed `/greetings/{id}` contracts produce
  identical external routes to the Minimal API sample.
- Introduced/retired contracts emit the exact active set for v1/v2.
- GET/POST/PUT/DELETE/command handler kinds emit the correct trigger method.
- Stable source snapshot proves attributes, method signature, async/await and typed
  dispatch call.
- Duplicate expanded route and sanitized Function-name collision diagnostics.
- A selected `AcceptsMessagePack = true` contract produces a stable error diagnostic
  and no generated Function; an explicitly excluded one produces no Functions
  diagnostic.
- Route guard tests keep the generated Function inventory (names, verbs, routes)
  explicit and reviewed; API-surface snapshots stay contract-only.

## Outcomes

- Every supported `[HttpEndpoint]` becomes a discoverable isolated-worker HTTP
  trigger with the same public verb, route and version lifetime.
- Generated methods contain no business, binding or exception logic.

## Acceptance

- [x] Every `[HttpEndpoint]` selected into a Functions host has an expected generated Function route in a test fixture (`FunctionsRouteGuardTests` in the boundary suite; framework tests live in `tests/`, samples only exemplify Application testing).
- [x] Function names and routes are deterministic across repeated generator runs.
- [x] Unsupported/duplicate contracts fail at compile time with stable diagnostics.
- [x] Selected MessagePack-enabled contracts fail at compile time; excluded
  MessagePack contracts do not block the host.
- [x] No Minimal API runtime type appears in generated Function source.
- [x] API-surface snapshots stay contract-only (`[HttpEndpoint]`); generated Functions are host wiring guarded by route guard tests, not snapshot entries (design decision, supersedes the original `FUNCTION` line requirement).
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.

> **Review 2026-09-02**: Still open: a fixture enumerating every sample endpoint's generated Function route, an explicit generator-determinism test, and API-surface snapshot entries for Function endpoints (snapshots record `[http=...]` routes only).
>
> **Review 2026-09-03**: Complete. Route fixture and HttpEndpoint-inventory cross-check live in `tests/Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Tests/FunctionsRouteGuardTests.cs` (framework tests must live in `tests/`, not samples). Determinism covered by the run-twice byte-identical generator test in `GeneratorSnapshotTests`. The API-surface snapshot requirement was superseded by design: generated Functions are host wiring, not contract surface — `[HttpEndpoint]` remains the only contract surface in snapshots.
