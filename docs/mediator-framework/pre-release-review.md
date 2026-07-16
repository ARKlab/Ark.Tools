# Mediator Framework Pre-Release Review — Analysis & Decisions

Status: **Approved analysis** — decisions recorded 2026-07-16.
Task breakdown: see [`tasks/README.md`](tasks/README.md).

## Part 1 — Adversarial review: implementation vs design (DX + Security)

### 1a. Design-vs-implementation divergences

| # | Divergence | Evidence |
|---|---|---|
| A1 | Generators are **not incremental** — full walk of every referenced assembly via `CompilationProvider`, contra `design.md` (syntax-provider pipeline). Re-runs on every keystroke. | `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi.Generators/MinimalApiEndpointGenerator.cs`, `.../Grpc.Generators/GrpcEndpointGenerator.cs`, `.../Rebus.Generators/RebusEndpointGenerator.cs` |
| A2 | Rebus duplicate/conflicting-owner diagnostics promised in design — only `ARKMF004` (blank queue) exists | `RebusEndpointGenerator.cs` |
| A3 | MessagePack missing-resolver "startup-time check" not implemented — 500 at first request instead | `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/ArkMessagePackEx.cs` |
| A4 | ProblemDetails mapping not reused from `Ark.Tools.AspNetCore` and reduced — `UnauthorizedAccessException` → 500 instead of 403 | `samples/.../Ark.MediatorFramework.Sample.WebInterface/ProblemDetailsExceptionHandler.cs` |
| A5 | gRPC user-context interceptor doesn't exist; sample substitutes an `IHttpContextAccessor` hack labeled "sample simplification" | `samples/.../Ark.MediatorFramework.Sample.WebInterface/HostUserContextProvider.cs` |
| A6 | gRPC streaming upload hand-written, not generated; **sample-specific `Documents.proto` hardcoded into the framework generator** — leaks into every consumer's proto export | `GrpcEndpointGenerator.cs`, `DocumentsGrpcService.cs` |
| A7 | `ICommand` in design but no generator handles it | all 3 generators match only `IRequest<>`/`IQuery<>` |
| ~~A8~~ | ~~Dev-only gating for Swagger/Scalar/gRPC-reflection not implemented — mapped unconditionally~~ **Struck (2026-07-16)**: mapping doc UIs unconditionally is accepted; endpoints are protected by default (SEC-01), doc UIs may stay anonymous. `design.md` updated. | `SampleStartup.cs` |
| A9 | `migration-from-mvc.md` says "keep authorization metadata on the generated contract or registration" — impossible today: no policy member on `HttpEndpointAttribute`, no `RouteHandlerBuilder`/group hook exposed | `HttpEndpointAttribute.cs`, `MinimalApiEndpointGenerator.cs` |
| A10 | Rebus wrapper drops `CancellationToken` — graceful shutdown never reaches handlers | `RebusEndpointGenerator.cs` |

### 1b. Developer Experience issues (severity-ordered)

- **High — No per-endpoint customization**: `MapArkEndpoints` is monolithic; can't add `RequireAuthorization`, filters, rate-limiting, output caching, CORS to a single endpoint.
- **High — Silent generator failures**: typo'd verb (`"GTE"`) silently maps to **POST**; non-`IRequest`/`IQuery` types with the attribute silently skipped; unmatched route placeholders silently dropped.
- **High — Non-record contracts break compilation inside `.g.cs`**: `body with { }` requires records; multipart requires settable props. Cryptic CS errors in generated code; only `ARKMF001` exists in the whole MinimalApi generator.
- **Medium — No startup handler verification**: missing handler registration = 500 at first request (contra design "validated at startup").
- **Medium — Fixed 200-only semantics**: `TypedResults.Ok` always; `null` result serializes as `200 null`.
- **Low**: namespace split (`Ark.MediatorFramework` attributes vs `Ark.Tools.MediatorFramework.*` runtime); diagnostic IDs 002/003 unaccounted; MessagePack 400 lacks ProblemDetails body.

### 1c. Security foot-guns (developer-mistake focus)

| # | Sev | Foot-gun | Mitigation |
|---|---|---|---|
| C1 | **Critical** | Generated endpoints are anonymous by default; sample survives only via a host `FallbackPolicy` nothing requires. | Both: attribute-level auth metadata with `AllowAnonymous` opt-out **and** route-group host policy hook. |
| C2 | High | `UseAuthorization` gated by `UseWhen(path "/api" \|\| Content-Type "application/grpc")` — gRPC auth hangs on an attacker-controlled header. | Unconditional `UseAuthorization()`. Doc UIs may remain anonymous: endpoints are protected by default (SEC-01) unless explicitly `AllowAnonymous`. |
| C3 | High | MessagePack deserializes untrusted bodies without `MessagePackSecurity.UntrustedData`. | One-line fix in `ArkMessagePackEx.GetOptions`. |
| C4 | High | Mass-assignment by design: contract = body envelope; server-owned properties are client-writable; GET/DELETE bind every property from query string. | `[ServerSet]`/`[NeverBind]` attribute honored by generator + OpenAPI schema. |
| C5 | Med-High | Dual `[HttpEndpoint]`+`[RebusMessage]` contracts run the same handler from the bus with a header-supplied principal, no policy check. | Transport-agnostic authorization decorator. |
| C6 | Med | Multipart: bypasses antiforgery metadata, no size/content-type limits, attacker-controlled `FileName` flows raw into `ArkAttachment.Name`. | Deliberate antiforgery handling, limits, filename sanitization. |
| C7 | Med | Error mapping reflects and serializes all public props of `BusinessRuleViolation` subtypes; `DocumentsGrpcService` echoes raw exception messages. | Opt-in extension serialization; never echo generic exception messages. |
| C8 | Med | `IntegrationTests` env-var swaps prod pipeline to a JWT scheme with a hardcoded symmetric key in source; malformed bearer → unhandled exception, not 401. | Try/catch token parse now; `WebApplicationFactory` substitution recorded as future improvement. |

## Part 2 — ReferenceProject vs MediatorFramework.Sample feature gaps

| Gap | Description | Scope |
|---|---|---|
| G1 | ICommand support | FRAMEWORK |
| G2 | FluentValidation via SimpleInjector decorators | SAMPLE (+ framework helper) |
| G3 | HTTP status semantics (404/202/204) with host customization | FRAMEWORK |
| G4 | SQL/Dapper + transactional Outbox | SAMPLE |
| G5 | Persisted auditing | SAMPLE |
| G6 | Optimistic concurrency + ETag | SAMPLE |
| G7 | Paging | SAMPLE |
| G8 | Policy authorization decorators | SAMPLE + FRAMEWORK |
| G9 | App Insights, config layering, IClock/NodaTime demo, richer test infra | SAMPLE |
| G10 | File download | SAMPLE + FRAMEWORK |

Reverse gaps (sample > ReferenceProject, keep as-is): gRPC code-first + proto export, MessagePack negotiation, single-handler/three-transports, `IntroducedIn`/`RetiredIn` versioning, polymorphic contracts, dead-letter demo, attachment abstraction.

## Part 3 — ASP.NET Core (≤ .NET 10) improvements

| # | Feature | Verdict |
|---|---|---|
| N1 | Minimal APIs built-in validation | Superseded by decision D2 (FluentValidation decorator authoritative). |
| N2 | `RequireAuthorization` on route groups | Folded into SEC-01. |
| N3 | OpenAPI 3.1 + XML-doc population + YAML | **Release blocker** — task NET-01. |
| N4 | Endpoint-specific OpenAPI operation transformers | Task NET-02, after FW-02. |
| N5 | Server-Sent Events transport | Post-release spike — NET-05. |
| N6 | JSON+PipeReader deserialization | Verification only — future improvements. |
| N7 | System.Text.Json JSON Patch / PATCH verb | Task NET-03, post-release. |
| N8 | Auth + Identity metrics | Task NET-04, pairs with G9. |
| N9 | Cookie login redirect avoidance | N/A bearer-only; note in migration doc — future improvements. |
| N10 | `RedirectHttpResult.IsLocalUrl`, `.localhost` TLD, memory-pool eviction | No action. |

## Decisions (2026-07-16)

| # | Question | Decision |
|---|---|---|
| D1 | C1 mitigation shape | **Both**: (a) generator emits `RequireAuthorization()` by default with per-attribute `AllowAnonymous` opt-out, **and** (b) `MapArkEndpoints` maps into a `RouteGroupBuilder` the host can configure. Sample must use `RequireAuthenticatedUser` as the default authorization policy. |
| D2 | Validation layer | Transport-agnostic **FluentValidation decorator** as recommended and demonstrated by `Ark.ReferenceProject` (`Query/Request/CommandFluentValidateDecorator` + conditional `NullValidator<>`). |
| D3 | G3 customization point | **Attribute customizations** on `HttpEndpointAttribute` (compile-time hints, AoT/doc-friendly), not a runtime mapper service. |
| D4 | ICommand HTTP semantics | **`[RebusMessage]`-based alternative**: commands that also carry `[RebusMessage]` (truly async) → `202 Accepted`; synchronously-completed commands → `204 No Content`. |
| D5 | ProblemDetails setup | **New shared package**; `Ark.Tools.AspNetCore` references it so existing behavior is preserved as-is. |
| D6 | C8 env-var auth scheme | Env var is acceptable for now; `WebApplicationFactory`-based scheme substitution recorded in [Future improvements](future-improvements.md). Malformed-bearer 401 handling is still fixed (SEC-08). |
| D7 | Release gating | **All G1–G10 are release blockers.** All security items (SEC-01..SEC-08) are release blockers. **N3 (XML-docs to OpenAPI) is a release blocker.** Remaining N items are post-release. |

## Priority order

1. Security defaults (SEC-01..SEC-04, GEN-04) — blockers, do first.
2. FW-01 ICommand + FW-02 status semantics — wire-shape changes; do before more consumers exist.
3. Generator diagnostics (GEN-01..GEN-03, GEN-05, GEN-06).
4. SMP-01 FluentValidation + SEC-05 authorization decorator — the transport-agnostic cross-cutting story.
5. Sample parity (SMP-02..SMP-06), FW-03, FW-04 and NET-01 (blocker).
6. Post-release: NET-02..NET-05, future improvements.
