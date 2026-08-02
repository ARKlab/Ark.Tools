# Mediator Framework ASP.NET Core hosting gap analysis

Status: **review revisions applied**. Analysis performed 2026-08-02 against .NET
10 and the repository state on this branch. This document records accepted
directions and proposed implementation work; it does not change runtime behavior.

## How to review

- Approve the recommended choice, select an alternative, or add a deployment
  constraint for each `HSD-*` decision.
- Do not start a task while one of its `Blocked by` decisions is unresolved.
- Decide observable behavior and package ownership here before introducing public
  APIs or dependencies.
- A task may be split into framework and sample PRs, but its acceptance criteria
  remain the delivery contract.

## Scope and baselines

The framework comparison covers
`Ark.Tools.MediatorFramework.MinimalApi` and its generated host surface against
`Ark.Tools.AspNetCore` and relevant subpackages. A capability demonstrated only
by `Ark.MediatorFramework.Sample.WebInterface` is not an Ark default: every
reusable default supplied by the existing startup libraries but absent from the
Minimal API hosting surface is recorded as a gap. The sample comparison against
`Ark.Reference.Core.WebInterface` determines how to demonstrate optional
application composition after those library gaps are identified.

Key evidence:

- `ArkStartupWebApi` composes `ArkStartupBase` and
  `ArkStartupWebApiCommon`, so the ReferenceProject receives the Ark security,
  telemetry, MVC, health and middleware defaults through inheritance
  (`src/aspnetcore/Ark.Tools.AspNetCore/Startup/ArkStartupWebApi.cs`).
- The mediator sample owns its composition in `SampleStartup` and calls it
  manually from top-level `Program.cs`
  (`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/`).
- Generated HTTP and gRPC mapping already checks the exact closed handler
  registrations. Rebus registration does the same
  (`MinimalApiEndpointGenerator.cs`, `GrpcEndpointGenerator.cs`,
  `RebusEndpointGenerator.cs`). This is narrower than a complete
  `Container.Verify()`.
- `SampleBusHostedService.StartAsync` currently calls `Container.Verify()` and
  then starts Rebus. The verification guarantee is therefore owned by a
  sample-specific bus service rather than the web-host composition contract.
- `ArkStartupWebApiCommon` exposes two `RegisterContainer` hooks, but it does
  **not** call `Container.Verify()`. `UseSimpleInjector` and container locking
  are not equivalent to full verification.

Authoritative 2026 checks used for the verdicts:

- [.NET Generic Host](https://learn.microsoft.com/dotnet/core/extensions/generic-host)
  still invokes `IHostedService.StartAsync` as part of host startup. A thrown
  `StartAsync` exception is therefore a startup failure, not a post-start
  background failure. The remaining issue is ownership, ordering and
  testability, not that `IHostedService` exceptions are ignored.
- [HTTPS enforcement in ASP.NET Core 10](https://learn.microsoft.com/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0)
  retains HSTS, while noting that API-only projects may instead listen only on
  HTTPS and that reverse proxies may own HSTS.
- [Forwarded headers in ASP.NET Core 10](https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0)
  supports `X-Forwarded-Prefix` and requires explicit trust configuration for
  proxies and networks.
- [ASP.NET Core 10 health checks](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)
  retain separate readiness and liveness semantics.
- [ASP.NET Core 10 response compression](https://learn.microsoft.com/aspnet/core/performance/response-compression?view=aspnetcore-10.0)
  remains useful but warns about compression over HTTPS and BREACH.
- Microsoft provides a current
  [migration path from the classic Application Insights SDK to Azure Monitor
  OpenTelemetry](https://learn.microsoft.com/azure/azure-monitor/app/migrate-to-opentelemetry)
  and documents `Azure.Monitor.OpenTelemetry.AspNetCore` for new ASP.NET Core
  instrumentation.
- The repository's NLog integration remains current:
  `ConfigureNLog` installs NLog as the `Microsoft.Extensions.Logging` provider
  and registers an `AppDomain.UnhandledException` fallback. NLog's maintained
  [hosting integration](https://github.com/NLog/NLog.Extensions.Logging)
  still requires deliberate host registration and shutdown flushing.

## Gap register

| Gap | Current evidence | Needed in 2026? | Disposition |
| --- | --- | --- | --- |
| **HSG-01 — No reusable Minimal API host composition contract** | `SampleStartup` manually repeats `AddSimpleInjector`, container-locking registration, `UseSimpleInjector`, authentication/authorization defaults and endpoint ordering. A sample is not a reusable Ark default. | **Yes.** Modern hosting removes the need for an inheritance-heavy `Startup` base, not for a consistent composition contract. | Design optional composable host defaults, including the existing secure authentication/authorization baseline; do not copy MVC/OData startup wholesale. |
| **HSG-02 — Complete SimpleInjector verification is coupled to Rebus startup** | Generated mapping verifies mediator handlers, while `SampleBusHostedService.StartAsync` performs full `Container.Verify()`. A host without that service has no full-composition guarantee. | **Yes.** The framework must build and verify the container during startup after registration is complete and before any request. It must not inspect or start external services. | Make verification an explicit framework host-composition phase. Separately demonstrate in the sample that its one-way Rebus bus starts before requests can be handled. |
| **HSG-03 — No Ark-default security-header/HSTS profile** | `ArkStartupBase` registers NetEscapades default API/Swagger policies, removes `Server`, and calls `UseSecurityHeaders` plus `UseHsts`. The mediator sample does none of these. | **Yes.** Ark hosting does not permit TLS-free deployment, including inside an mTLS service mesh. | Provide an optional Ark-default startup helper that enables the current WebApiCommon security headers and HSTS behavior. Direct composition without the helper remains supported. |
| **HSG-04 — No strict path-base profile** | `ArkStartupBase` accepts custom `X-Forwarded-PathBase` without validating the value. The mediator sample has no equivalent. | **Yes for prefixed deployments.** The path prefix is application configuration, not a reason to trust request-controlled forwarding metadata. | Accept an explicitly configured, strictly validated absolute path prefix and apply it as `PathBase`; do not trust proxy/network headers for this feature. |
| **HSG-05 — No default health endpoint** | `ArkStartupWebApiCommon` adds and maps `/healthCheck`; the mediator sample exposes no health endpoint. The existing optional UI/history registration is separate from the endpoint contract. | **Yes.** Preserve the established `/healthCheck` endpoint contract as an Ark default. | Include only the endpoint by default. Do not register or expose HealthChecks UI or history by default. |
| **HSG-06 — Response compression default is missing** | `ArkStartupWebApiCommon` enables Brotli/Gzip and `EnableForHttps = true`; the mediator sample has no response-compression middleware. | **Yes.** The existing response-compression default is accepted despite the documented BREACH trade-off. | Enable response compression by default, retain gRPC compression when supported, and bypass HTTP compression only for streaming responses when buffering would delay emitted items. |
| **HSG-07 — Application Insights parity is partial** | `Program.cs` already calls `AddApplicationInsithsTelemetryForWebHostArk`; therefore “Application Insights is absent” is inaccurate. Unlike `ArkStartupBase`, the sample omits `WebApiUserTelemetryInitializer`, `WebApi4xxAsSuccessTelemetryInitializer`, other classic Ark defaults and SimpleInjector integration. | **Yes.** Complete the classic setup now, treating 4xx responses as client outcomes rather than server failures. | Reuse the current Ark setup without duplicate registration and skip only Snapshot Debugger. OpenTelemetry migration is future work. |
| **HSG-08 — NLog is not registered in the mediator sample host** | The ReferenceProject calls `ConfigureNLog`; the mediator `Program.cs` does not. Ark's helper is already compatible with `IHostBuilder` exposed by `WebApplicationBuilder.Host`. | **Yes for this repository's sample.** NLog is the Ark logging standard and is already installed transitively in the solution. No Minimal-API-specific NLog abstraction is needed. | Register the existing helper before build and verify that framework `ILogger` events reach NLog. |
| **HSG-09 — Main has no startup-failure log/flush boundary** | ReferenceProject `Main` catches fatal exceptions and shuts NLog down. Mediator top-level statements await `RunAsync` without a process boundary. Failures before or during `Build`, `SampleStartup.Configure`, verification or `RunAsync` have no explicit fatal NLog event or guaranteed flush. | **Yes.** Async targets can lose final events without shutdown. The ReferenceProject pattern needs one correction: swallowing the exception can produce a successful process exit. | Add an explicit async `Main` or equivalent helper, structured fatal logging with invariant culture, shutdown/flush in `finally`, and deliberate non-zero failure semantics. |
| **HSG-10 — Production entry-point wiring is not tested** | Reqnroll creates a separate `HostBuilder`, calls `SampleStartup` directly and never executes mediator `Program.cs`. NLog, Key Vault ordering, telemetry host registration and the process failure boundary can regress while transport tests stay green. | **Yes.** Composition-root tests are the smallest check for operational wiring. External telemetry and Key Vault calls must remain replaceable/inert. | Add a host-construction seam and focused tests; use a subprocess only where process exit and final flush cannot be asserted in-process. |
| **HSG-11 — Startup-error capture is not enabled by an Ark Minimal API default** | The ReferenceProject enables `CaptureStartupErrors(true)` and detailed errors so grave startup failures are diagnosable during smoke tests. The mediator host has no reusable equivalent. | **Yes.** Deferring diagnostics to process logging alone makes early host failures unnecessarily costly to diagnose. | Add startup-error capture and detailed startup errors to the optional Ark host defaults, with tests proving failures remain visible and fail startup. |

## Confirmed non-gaps and rejected ports

These differences should not become parity work unless a consumer requirement is
provided:

| Capability | Finding |
| --- | --- |
| Handler registration validation | Completed by GEN-03 for generated HTTP, gRPC and Rebus mappings. HSG-02 covers full container verification only. |
| Application Insights registration | Already present in mediator `Program.cs`; HSG-07 concerns the telemetry strategy and missing host/application integration. |
| ProblemDetails | The mediator sample uses the shared Ark exception handler and standard generated responses. Do not reintroduce MVC exception filters. |
| OpenAPI/versioning | Native OpenAPI 3.1, generated version expansion and Scalar are deliberate replacements for Swashbuckle plus `Asp.Versioning`. |
| OData and MVC filters/model binders | Controller-specific and outside a generated Minimal API host profile. |
| Permissive CORS from `ArkStartupWebApiCommon` | `AllowCredentials`, every origin, every method/header and exposed `*` is not a safe default to port. CORS must be application opt-in with named origins. |
| Static files | Not hosting them is an accepted, safer Minimal API default. Applications may opt in explicitly. |
| Request localization | Application concern, not an API host default. Applications may opt in explicitly. |
| Request-controlled `X-Forwarded-PathBase` middleware | Replaced by strict configured-prefix handling; see HSG-04. |
| `GlobalInit.InitStatics()` | The mediator sample already registers NodaTime JSON/protobuf support and injects `IClock`. Process-wide culture mutation and ReferenceProject-specific Dapper setup should not be copied. |

### ASP.NET Core subpackage disposition

| Package area | Mediator disposition |
| --- | --- |
| `Ark.Tools.AspNetCore.ApplicationInsights` | Used by the sample, with the generation and integration questions recorded as HSG-07/HST-07. |
| `Ark.Tools.AspNetCore.HealthChecks` | Preserve its `/healthCheck` endpoint contract in the Ark defaults without enabling its optional UI/history support; see HSG-05/HST-04. |
| `Ark.Tools.AspNetCore.MessagePack` | Already used by the sample. Mediator-specific resolver security and generated content negotiation are covered by completed framework work. |
| `Ark.Tools.AspNetCore.ProblemDetails` | Already used through the shared Ark exception-handler package. Controller filters and conventions are not required. |
| `Ark.Tools.AspNetCore.Swashbuckle` | Replaced by native OpenAPI 3.1 generation, Scalar and the existing Swagger UI compatibility view. No parity task. |
| `Ark.Tools.AspNetCore.CommaSeparatedParameters` | MVC value-provider behavior is not a general Minimal API requirement. Add contract-level collection binding only when a concrete wire contract requires it. |
| `Ark.Tools.AspNetCore.Auth0` and basic-auth proxy packages | Identity-provider and proxy deployment choices, not host defaults. The sample's Entra ID/B2C bearer configuration remains the relevant comparison. |
| `Ark.Tools.AspNetCore.NestedStartup` | Controller-area branching infrastructure, not applicable to the generated endpoint host. |
| `Ark.Tools.AspNetCore.RavenDb` | OData/RavenDB-specific behavior; intentionally outside mediator hosting. |
| `Ark.Tools.AspNetCore` startup classes | Security, proxy, health, compression, DI lifecycle and operational startup findings were separated above. MVC, OData, localization, CORS and static-file differences were rejected as blanket parity work. |

## Proposed decisions

### HSD-01 — Hosting API shape and package ownership

- **Status:** ACCEPTED
- **Recommended:** add composable service, application-builder and container
  verification extensions in a small ASP.NET Core hosting package that can be
  consumed by Minimal API hosts. Keep mediator endpoint generation independent
  from the operational host profile. Avoid inheriting from
  `ArkStartupWebApiCommon`, which would pull MVC, OData and Swashbuckle policy
  into a generated Minimal API host.
- **Alternative A:** place the profile directly in
  `Ark.Tools.MediatorFramework.MinimalApi`.
- **Alternative B:** make the sample inherit from `ArkStartupWebApi`.
- **Blocked tasks:** HST-01, HST-02, HST-03.

### HSD-02 — Container registration hooks and verification boundary

- **Status:** ACCEPTED
- **Recommended:** expose two deliberate phases equivalent to the useful parts
  of `RegisterContainer()` and `RegisterContainer(IServiceProvider)`, then
  synchronously complete `Container.Verify()` after `UseSimpleInjector` and all
  registrations but before the host accepts requests. Framework verification
  does not inspect, register or start external services. The sample independently
  starts its one-way Rebus bus during host startup before requests can resolve
  `IBus`.
- **Blocked tasks:** HST-01, HST-04.

### HSD-03 — Security policy ownership

- **Status:** ACCEPTED
- **Recommended:** the optional Ark startup helper enables security headers and
  HSTS in the same way as `ArkStartupBase` today, including named treatment for
  Scalar, Swagger UI and gRPC reflection. TLS-free hosting is unsupported even
  inside an mTLS service mesh. Applications may continue to compose the host
  directly without selecting the Ark defaults.
- **Blocked tasks:** HST-02.

### HSD-04 — Trusted proxy contract

- **Status:** ACCEPTED
- **Recommended:** the default Ark startup profile accepts any configured path
  prefix that passes strict validation; applications may opt out. Normalize and
  validate the prefix as an absolute path-only value: it starts with one `/`, is
  not `/`, has no trailing slash, query, fragment, scheme, authority, backslash,
  control character, empty segment, dot segment, encoded slash/backslash or
  percent-encoded dot segment. Reject invalid configuration at startup. Do not
  bind the feature to known proxies/networks or trust a request header.
- **Blocked tasks:** HST-03.

### HSD-05 — Health endpoint contract

- **Status:** ACCEPTED
- **Recommended:** preserve the ReferenceProject `/healthCheck` endpoint path
  and response contract. Include the endpoint in the Ark defaults, but do not
  register or map HealthChecks UI, in-memory history or UI assets by default.
- **Blocked tasks:** HST-04.

### HSD-06 — Compression policy

- **Status:** ACCEPTED
- **Recommended:** copy the existing Brotli/Gzip HTTPS setup into the optional
  Ark defaults despite the accepted BREACH trade-off. Keep gRPC compression
  enabled when supported. Exclude streaming HTTP responses only when required to
  prevent compressor buffering from delaying response items; cover first-item
  delivery with a runnable test.
- **Blocked tasks:** HST-05.

### HSD-07 — Telemetry generation

- **Status:** ACCEPTED
- **Recommended:** complete the classic Application Insights setup supplied by
  Ark today, including user telemetry, `WebApi4xxAsSuccessTelemetryInitializer`,
  processors, dependency settings and SimpleInjector integration. Skip only
  Snapshot Debugger. Preserve the convention that 4xx responses are not server
  failures. OpenTelemetry migration remains future work.
- **Blocked tasks:** HST-07.

### HSD-08 — Fatal exit semantics

- **Status:** ACCEPTED
- **Recommended:** log once at `Fatal`, attempt console fallback, flush/shut down
  NLog in `finally`, and preserve a non-zero exit by rethrowing after logging.
  Avoid duplicate fatal events between `Main` and
  `AppDomain.UnhandledException`.
- **Alternative:** return an explicit non-zero exit code.
- **Blocked tasks:** HST-06.

### HSD-09 — Sample intent

- **Status:** ACCEPTED
- **Recommended:** keep the mediator sample runnable without Azure or SQL while
  accepting local Azurite and local SQL Server containers as sample/test
  dependencies. No live Azure resource, externally hosted database, production
  secret or external telemetry call is required by tests.
- **Blocked tasks:** HST-04, HST-06, HST-07, HST-08.

### HSD-10 — Startup diagnostics

- **Status:** ACCEPTED
- **Recommended:** the optional Ark startup helper enables
  `CaptureStartupErrors(true)` and detailed startup errors to preserve smoke-test
  diagnostics. This complements structured fatal logging and non-zero exit
  semantics; it does not replace either.
- **Blocked tasks:** HST-09.

## Implementation tasks

Each executable task is maintained as a self-contained file:

| Task | Title |
| --- | --- |
| [HST-01](tasks/aspnetcore/HST-01-composable-minimal-api-startup.md) | Composable Minimal API startup |
| [HST-02](tasks/aspnetcore/HST-02-security-headers-hsts-profile.md) | Security headers and HSTS defaults |
| [HST-03](tasks/aspnetcore/HST-03-path-base-validation.md) | Strict path-base configuration |
| [HST-04](tasks/aspnetcore/HST-04-health-endpoint.md) | Default health endpoint |
| [HST-05](tasks/aspnetcore/HST-05-response-compression.md) | Default response compression |
| [HST-06](tasks/aspnetcore/HST-06-nlog-process-boundary.md) | NLog process boundary |
| [HST-07](tasks/aspnetcore/HST-07-classic-application-insights.md) | Complete classic Application Insights defaults |
| [HST-08](tasks/aspnetcore/HST-08-composition-root-tests.md) | Production composition-root tests |
| [HST-09](tasks/aspnetcore/HST-09-startup-error-diagnostics.md) | Startup-error diagnostics |

## Suggested execution order

1. HST-01.
2. HST-02, HST-03, HST-04, HST-05 and HST-09.
3. HST-06 and HST-07.
4. HST-08.
