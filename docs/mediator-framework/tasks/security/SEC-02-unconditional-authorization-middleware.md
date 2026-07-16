# SEC-02 — Unconditional authorization middleware + environment-gated doc UIs (C2 + A8)

**Category**: security · **Priority**: Release blocker · **Scope**: SAMPLE (pattern documented for hosts)
**Depends on**: SEC-01 (recommended first).

## Problem

`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/SampleStartup.cs`
gates `UseAuthorization()` behind `app.UseWhen(...)` matching request path prefix `/api` **or**
`Content-Type: application/grpc`:

- OpenAPI documents, Scalar/Swagger UIs and gRPC reflection are served **anonymously in every environment** (also design divergence A8: no dev-only gating exists).
- The gRPC branch keys off a client-controlled header, so authorization for gRPC hangs on attacker-controlled input.

## Steps

1. In `SampleStartup.cs` `Configure`: remove the `UseWhen` wrapper; call `app.UseAuthentication()` and `app.UseAuthorization()` unconditionally, in the standard order (after routing, before endpoint mapping).
2. Gate developer surfaces by environment:
   - `MapOpenApi()`, Scalar/Swagger UI endpoints, and `MapGrpcReflectionService()` — map only when `env.IsDevelopment()` **or** protect them with `.RequireAuthorization()` in non-Development (choose: map in Development anonymously, in non-Development require auth; document the choice inline). Note: the repo recently added production grpcui support (see `samples/Ark.MediatorFramework.Sample/README.md`) — keep reflection reachable in production but **behind authorization**, not anonymous.
3. Verify the Reqnroll suite (`samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests`) still passes — tests already send bearer tokens for gRPC.
4. Add tests: unauthenticated GET of the OpenAPI document path in the test (non-Development) environment → 401; gRPC call without metadata → `Unauthenticated`.

## Outcomes

- Authorization middleware always runs; no path/header-conditional security.
- Documentation/reflection surfaces are environment-gated or authenticated, never anonymous in production.

## Acceptance

- [ ] No `UseWhen` around `UseAuthorization` remains in the sample.
- [ ] OpenAPI/Scalar/reflection endpoints: anonymous only in Development; authorized otherwise (tests prove the non-Development behavior).
- [ ] gRPC call without bearer metadata fails `Unauthenticated` (test).
- [ ] Full solution build + tests green (see tasks/README.md gates).
- [ ] `design.md` dev-gating section updated to match reality.
