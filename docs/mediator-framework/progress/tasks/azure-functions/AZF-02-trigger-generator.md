# AZF-02 — HTTP trigger generation, routing and version expansion

**Category**: azure-functions · **Priority**: core · **Scope**: GENERATOR

## Problem

Azure Functions discovers HTTP endpoints only from `[Function]` and
`[HttpTrigger]` metadata. Minimal API route mapping cannot execute in the isolated
Functions host, so every active mediator HTTP route needs deterministic trigger
source.

## Prerequisites

- AZF-01 merged.
- AZD-02, AZD-03 and AZD-09 decided.
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
   handler kinds. Invalid endpoints emit no partial source.
9. Add the generated external route to API-surface snapshot output using a stable
   `FUNCTION` line that includes Function name, verb, route and version.

## Caveats

- ASP.NET Core integration supplies `HttpRequest`; do not emit `HttpRequestData`.
- Do not emit endpoint routing, route groups, middleware calls, Minimal API result
  types or `MapArkEndpointsFromAssembly`.
- Azure Functions `host.json` route prefix is host-wide. Generated contract routes
  must not rely on per-function host-prefix variation.
- Function discovery metadata must consist of compile-time constants.
- Keep MessagePack flags out of the emitted trigger; JSON remains available and
  MessagePack is out of scope.

## Required test coverage

- Explicit `/api/v{version}` and host-prefixed `/greetings/{id}` contracts produce
  identical external routes to the Minimal API sample.
- Introduced/retired contracts emit the exact active set for v1/v2.
- GET/POST/PUT/DELETE/command handler kinds emit the correct trigger method.
- Stable source snapshot proves attributes, method signature, async/await and typed
  dispatch call.
- Duplicate expanded route and sanitized Function-name collision diagnostics.
- `AcceptsMessagePack = true` does not suppress JSON trigger generation or emit
  MessagePack handling.
- API-surface snapshot changes are explicit and reviewed.

## Outcomes

- Every supported `[HttpEndpoint]` becomes a discoverable isolated-worker HTTP
  trigger with the same public verb, route and version lifetime.
- Generated methods contain no business, binding or exception logic.

## Acceptance

- [ ] Every sample `[HttpEndpoint]` has an expected generated Function route in a test fixture.
- [ ] Function names and routes are deterministic across repeated generator runs.
- [ ] Unsupported/duplicate contracts fail at compile time with stable diagnostics.
- [ ] No Minimal API runtime type appears in generated Function source.
- [ ] API-surface snapshots record Function endpoints.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
