# SEC-04 — `[ServerSet]` binding protection against mass assignment (C4)

**Category**: security · **Priority**: Release blocker · **Scope**: FRAMEWORK

## Problem

A mediator contract is simultaneously the HTTP body/query envelope, the gRPC message and the bus
message. Adding a server-owned property (`TenantId`, `UserId`, `IsAdmin`, ...) to a contract —
natural, because handlers and bus consumers need it — silently makes it **client-writable**:
- Body-bound verbs: every settable property deserializes from the body.
- GET/DELETE: the generator binds **every** property from route/query string.

There is no way to mark a property as server-populated, and no warning.

Files: `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/HttpEndpointAttribute.cs`
(binding attributes live next to `BindFromQueryAttribute`),
`src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi.Generators/MinimalApiEndpointGenerator.cs`.

## Steps

1. Add `ServerSetAttribute` (property-level, `AttributeUsage(AttributeTargets.Property)`) in the same file/namespace as `BindFromQueryAttribute`. XML doc: "Property is populated server-side; never bound from client input and excluded from OpenAPI request schemas."
2. MinimalApi generator:
   - Route/query binding (GET/DELETE and `[BindFromQuery]` paths): skip `[ServerSet]` properties entirely.
   - Body binding: after deserialization, emitted code must reset `[ServerSet]` properties to their default (`body = body with { Prop = default }` for records, or property assignment) so client-supplied values can never flow into the handler. Emit a `ARKMFxxx` diagnostic (error) if a `[ServerSet]` property on a body-bound record has no accessible `init/set` needed for the reset.
   - OpenAPI: exclude `[ServerSet]` properties from the request schema (via a schema transformer registered by the framework, or `[JsonIgnore]`-equivalent metadata emission — pick the least invasive that works with `Microsoft.AspNetCore.OpenApi`).
3. gRPC generator: exclude `[ServerSet]` properties from the generated request proto messages (they remain in responses if present there).
4. Heuristic analyzer diagnostic (warning, suppressible): contract property named `TenantId`, `UserId`, `IsAdmin`, `Role`, `Roles` **without** `[ServerSet]` on an `[HttpEndpoint]` contract → warn "possible mass-assignment; mark [ServerSet] or suppress".
5. Sample: add one `[ServerSet]` usage (e.g. an audit/user property on an existing greeting contract populated by a decorator or handler from `IContextProvider<ClaimsPrincipal>`).
6. Tests:
   - HTTP test posting a body that sets the `[ServerSet]` property → handler observes default/server value, not the client value.
   - GET binding test: `[ServerSet]` property absent from query binding and from the OpenAPI document request schema.
   - Generator diagnostic tests for the heuristic warning.

## Outcomes

- Contracts can declare server-owned properties that are provably unbindable from any client input and invisible in request schemas.
- Suspicious unmarked property names produce a build-time warning.

## Acceptance

- [ ] `[ServerSet]` exists, documented, honored by MinimalApi (route, query, body) and gRPC request messages.
- [ ] Client-supplied value for a `[ServerSet]` property never reaches the handler (integration test).
- [ ] `[ServerSet]` properties absent from OpenAPI request schema (test inspects the document).
- [ ] Heuristic diagnostic fires on `TenantId`/`IsAdmin`-style unmarked properties (generator test).
- [ ] Sample demonstrates the attribute.
- [ ] Full solution build + tests green; `design.md` updated.
