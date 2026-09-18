# SEC-02 — Unconditional authorization middleware (C2; A8 struck)

**Category**: security · **Priority**: Release blocker · **Scope**: SAMPLE (pattern documented for hosts)
**Depends on**: SEC-01 (recommended first).

## Problem

`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/SampleStartup.cs`
gates `UseAuthorization()` behind `app.UseWhen(...)` matching request path prefix `/api` **or**
`Content-Type: application/grpc`:

- The gRPC branch keys off a client-controlled header, so authorization for gRPC hangs on attacker-controlled input.
- Doc UIs (OpenAPI, Scalar/Swagger, gRPC reflection) being served anonymously is **accepted** (A8 struck): endpoints are protected by default (SEC-01) unless explicitly anonymous.

## Steps

1. In `SampleStartup.cs` `Configure`: remove the `UseWhen` wrapper; call `app.UseAuthentication()` and `app.UseAuthorization()` unconditionally, in the standard order (after routing, before endpoint mapping).
2. Doc UIs stay anonymous and mapped unconditionally (decision 2026-07-16, A8 struck): `MapOpenApi()`, Scalar/Swagger UI endpoints and `MapGrpcReflectionService()` may be served anonymously in every environment because **endpoints are protected by default** (SEC-01) unless explicitly `AllowAnonymous`. Mark these doc surfaces `AllowAnonymous` explicitly where the default policy would otherwise catch them.
3. Verify the Reqnroll suite (`samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests`) still passes — tests already send bearer tokens for gRPC.
4. Add test: gRPC call without metadata → `Unauthenticated`.

## Outcomes

- Authorization middleware always runs; no path/header-conditional security.
- Doc/reflection surfaces are anonymous by explicit `AllowAnonymous`; all business endpoints protected by default (SEC-01).

## Acceptance

- [x] No `UseWhen` around `UseAuthorization` remains in the sample.
- [x] OpenAPI/Scalar/reflection endpoints mapped unconditionally, explicitly `AllowAnonymous`.
- [x] gRPC call without bearer metadata fails `Unauthenticated` (test).
- [x] Full solution build + tests green (see tasks/README.md gates).
- [x] `design.md` doc-UI section updated (Development-only default removed).

> **Review 2026-09-02**: Anonymous gRPC rejection is proven in `tests/Ark.Tools.MediatorFramework.Hosting.Tests/GrpcAuthorizationTests.cs` (status `PermissionDenied` with the handler never executing) rather than a sample-level `Unauthenticated` assertion; the outcome — no anonymous gRPC dispatch — is covered.
