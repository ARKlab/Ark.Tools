# SEC-08 — Malformed bearer → 401 in test auth scheme (C8, decision D6)

**Category**: security · **Priority**: Release blocker (bug part only) · **Scope**: SAMPLE

## Problem & decision

`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/Auth/AuthenticationEx.cs`
swaps the pipeline to a JWT scheme (with a symmetric key in source) when
`ASPNETCORE_ENVIRONMENT=IntegrationTests`.

Decision D6: the env-var switching pattern is **accepted for now**; migrating to
`WebApplicationFactory`-based scheme substitution is recorded in
[`../../future-improvements.md`](../../future-improvements.md). **Do not restructure the auth wiring in this task.**

The remaining bug: a **malformed bearer token** (not parseable as JWT) causes an unhandled exception
→ 500, instead of a clean `401 Unauthorized`.

## Steps

1. Locate the token parsing in the test scheme (`AuthenticationEx.cs` / associated handler). Wrap
   token read/validation in try/catch (or use `JwtBearerEvents.OnAuthenticationFailed` /
   `TokenHandler` result checks) so any parse/validation failure yields `AuthenticateResult.Fail(...)`
   → framework responds 401 with `WWW-Authenticate`, no exception escapes the middleware.
2. Verify the same for the production scheme path (missing or non-JWT `Authorization` header) — should already 401 via `JwtBearerHandler`; add the test anyway.
3. Tests (Reqnroll or MSTest in `Ark.MediatorFramework.Sample.Tests`):
   - Bearer-scheme header carrying a non-parseable JWT value → 401 (no 500).
   - `Authorization: Basic abc` → 401.
   - Missing header → 401 (existing behavior, keep covered).

## Outcomes

- Garbage credentials can never produce a 500 or an unhandled exception in the sample host.

## Acceptance

- [ ] Malformed bearer tests return 401 across the three cases above.
- [ ] No restructuring of the env-var auth pattern (per D6).
- [ ] Full solution build + tests green.
