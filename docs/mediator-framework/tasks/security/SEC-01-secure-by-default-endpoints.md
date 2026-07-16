# SEC-01 — Secure-by-default generated HTTP endpoints (C1 + A9 + N2, decision D1)

**Category**: security · **Priority**: Release blocker · **Scope**: FRAMEWORK + SAMPLE
**Depends on**: nothing — do this first (it changes the endpoint-emission shape other tasks build on).

## Problem

The MinimalApi generator emits endpoints with **zero authorization metadata**. The sample only stays
authenticated because `SampleStartup.cs` sets an `AuthorizationOptions.FallbackPolicy`. Any consumer
that copies `MapArkEndpoints()` into a plain host ships every contract — including mutations —
anonymous. There is also no way to attach a policy per endpoint (`HttpEndpointAttribute` has no
policy member, and no `RouteHandlerBuilder`/group hook is exposed).

Files:
- `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/HttpEndpointAttribute.cs`
- `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi.Generators/MinimalApiEndpointGenerator.cs`
- `samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/SampleStartup.cs`

## Decision (D1)

Apply **both** mitigations:
- (a) Attribute-level: generator emits `.RequireAuthorization(...)` per endpoint by default, with explicit opt-out and per-endpoint policy support on `HttpEndpointAttribute`.
- (b) Group-level: `MapArkEndpoints` maps all endpoints into a single `RouteGroupBuilder` and exposes it (or a configuration callback) so the host can apply cross-cutting config (auth, filters, rate limiting, CORS, output caching).

The sample must configure `RequireAuthenticatedUser` as the **default authorization policy** (`options.DefaultPolicy`), not only as fallback.

## Steps

1. Extend `HttpEndpointAttribute` (in `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/HttpEndpointAttribute.cs`) with:
   - `public string? Policy { get; set; }` — authorization policy name for this endpoint.
   - `public bool AllowAnonymous { get; set; }` — explicit opt-out; when `true` emit `.AllowAnonymous()`.
   - XML docs on both, stating the secure-by-default behavior.
2. In `MinimalApiEndpointGenerator.cs`, change the emitted `MapArkEndpoints` extension:
   - Signature: `public static RouteGroupBuilder MapArkEndpoints(this IEndpointRouteBuilder endpoints, Action<RouteGroupBuilder>? configure = null)`.
   - Emit `var group = endpoints.MapGroup(string.Empty);` then map every endpoint on `group`, invoke `configure?.Invoke(group)`, and `return group;`.
   - Per endpoint: if `AllowAnonymous == true` → append `.AllowAnonymous()`; else if `Policy` set → `.RequireAuthorization("<policy>")`; else → `.RequireAuthorization()`.
3. Update the sample:
   - In `SampleStartup.cs`, set `options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();` in `AddAuthorization` (keep the `FallbackPolicy` as defense-in-depth).
   - Mark any intentionally-public sample endpoint with `AllowAnonymous = true` on its `[HttpEndpoint]`, if one exists; otherwise none.
4. Update docs: `docs/mediator-framework/design.md` (endpoint mapping section) and `docs/mediator-framework/migration-from-mvc.md` (the "keep authorization metadata" claim now has a concrete mechanism).
5. Update/extend generator snapshot tests (if present under `tests/`) and the sample behavioral tests: existing Reqnroll tests in `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests` must keep passing (they use bearer tokens already).
6. Add a test proving the default: call one generated endpoint **without** a token and assert `401`; call one with a valid token and assert success. Add a test for `AllowAnonymous = true` if a sample endpoint uses it.

## Outcomes

- Generated endpoints require authorization by default; anonymity is an explicit, reviewable opt-in on the contract.
- Hosts get a `RouteGroupBuilder` hook for per-group customization (filters, rate limiting, CORS...), removing the "abandon the generator" escape hatch.
- Sample demonstrates `RequireAuthenticatedUser` as default policy.

## Acceptance

- [ ] `HttpEndpointAttribute` has `Policy` and `AllowAnonymous` with XML docs.
- [ ] Emitted code: every endpoint carries `.RequireAuthorization()` / `.RequireAuthorization(policy)` / `.AllowAnonymous()`; `MapArkEndpoints` returns the `RouteGroupBuilder` and accepts an optional `configure` callback.
- [ ] A host with **no** `FallbackPolicy` still returns `401` for unauthenticated calls to generated endpoints (covered by a test).
- [ ] Sample sets `DefaultPolicy` = `RequireAuthenticatedUser`.
- [ ] Unauthenticated request test → 401; authenticated → 2xx.
- [ ] `dotnet build Ark.Tools.slnx -c Debug` zero warnings; `dotnet test Ark.Tools.slnx --no-build -c Debug --minimum-expected-tests 1` green.
- [ ] `design.md` and `migration-from-mvc.md` updated.
