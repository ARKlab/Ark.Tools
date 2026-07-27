# Future Improvements (post-1.0, recorded by decision)

These items are explicitly **not** release blockers. Recorded here per the 2026-07-16 review decisions (see [pre-release-review.md](pre-release-review.md)).

## 1. `WebApplicationFactory`-based test auth scheme substitution (from C8/D6)

Today the sample switches to a JWT test scheme when `ASPNETCORE_ENVIRONMENT=IntegrationTests`
(`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/Auth/AuthenticationEx.cs`).
Decision D6: this env-var pattern is acceptable for now. Future improvement: replace it with
`WebApplicationFactory<TEntryPoint>.WithWebHostBuilder(...)` scheme substitution so the production
`Startup` contains zero test-only authentication branches and no symmetric key ships in source.

## 2. AoT / trimmed sample host

Prior research: `docs/aot-di-container-research.md` (Pure.DI primary, MEDI fallback, minimal-APIs-only,
zero per-handler registration required; Roslyn generators can't chain). Build a trimmed/AoT sample host
once FW-01/FW-02 and the generator diagnostics work stabilize — the MinimalApi transport is the only
AoT-viable one (Rebus/SimpleInjector are not trim-safe today). Schedule after SEC-01..SEC-04 to avoid
changing the endpoint-emission shape twice.

## 3. JSON + PipeReader deserialization verification (N6)

.NET 10 Minimal APIs deserialize JSON bodies via `PipeReader`. Verify generated endpoints benefit
automatically (they should — they use standard `HttpContext` JSON binding); add a perf note. No code
change expected.

## 4. Cookie login redirect note for API endpoints (N9)

Bearer-only sample is unaffected. Add a note to `migration-from-mvc.md` for cookie-auth hosts:
use the .NET 10 known-API-endpoint behavior so cookie middleware returns 401/403 instead of a login
redirect for `/api` routes.

## 5. Server-Sent Events transport

See task [tasks/aspnetcore/NET-05-sse-transport-spike.md](tasks/aspnetcore/NET-05-sse-transport-spike.md) — spike scheduled post-release.
