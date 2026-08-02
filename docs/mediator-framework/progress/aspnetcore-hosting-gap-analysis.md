# Mediator Framework ASP.NET Core hosting gap analysis

Status: **review required**. Analysis performed 2026-08-02 against .NET 10 and the
repository state on this branch. This document proposes work; it does not approve
the decisions below or change runtime behavior.

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
`Ark.Tools.AspNetCore` and relevant subpackages. The sample comparison covers
`Ark.MediatorFramework.Sample.WebInterface` against
`Ark.Reference.Core.WebInterface`.

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
| **HSG-01 — No reusable Minimal API host composition contract** | `SampleStartup` manually repeats `AddSimpleInjector`, container-locking registration, `UseSimpleInjector`, authentication and endpoint ordering. The reusable Minimal API package supplies endpoint features but no Ark host baseline or extension hooks. | **Yes.** Modern hosting removes the need for an inheritance-heavy `Startup` base, not for a consistent composition contract. | Design a composable host profile; do not copy the MVC/OData startup wholesale. |
| **HSG-02 — Complete SimpleInjector verification is coupled to Rebus startup** | Generated mapping verifies mediator handlers, while `SampleBusHostedService.StartAsync` performs full `Container.Verify()`. A host without that service has no full-composition guarantee. | **Yes, with corrected rationale.** `StartAsync` failures do fail host startup in .NET 10. The gap is that verification is optional, late in the composition sequence, coupled to bus startup, and absent from direct `SampleStartup.Configure` tests until the host starts. | Make verification an explicit host-composition phase after all MS DI cross-wiring and endpoint registrations and before serving requests. Keep bus startup separate. |
| **HSG-03 — No Ark-default security-header/HSTS profile** | `ArkStartupBase` registers NetEscapades default API/Swagger policies, removes `Server`, and calls `UseSecurityHeaders` plus `UseHsts`. The mediator sample does none of these. | **Yes, deployment-sensitive.** Headers remain relevant. HSTS may belong to Kestrel or the trusted edge, and API-only HTTPS deployments may not need middleware emission. | Provide an explicit Ark security profile with separately controllable headers and HSTS; do not silently double-own edge policy. |
| **HSG-04 — No trusted reverse-proxy/path-base profile** | `ArkStartupBase` directly trusts custom `X-Forwarded-PathBase`. The mediator sample has no equivalent. ASP.NET Core now has standard `X-Forwarded-Prefix` support and tightened trust rules. | **Yes for proxied deployments; the old workaround is not.** Blindly trusting a request header is unsuitable in 2026. | Use standard forwarded-header middleware with configured trusted proxies/networks. Do not port the custom header middleware as a default. |
| **HSG-05 — No readiness/liveness surface in the mediator sample** | `ArkStartupWebApiCommon` adds and maps `/healthCheck`; the mediator sample exposes no health endpoint. The existing Ark helper publishes to Application Insights and uses one aggregate endpoint. | **Yes for deployable samples.** Separate readiness/liveness is the current platform model. The old single UI-shaped response is not automatically the right public contract. | Demonstrate built-in health checks and decide paths, payload, authorization and dependency classification before reusing the Ark helper. |
| **HSG-06 — Response compression has no declared policy** | `ArkStartupWebApiCommon` enables Brotli/Gzip and `EnableForHttps = true`; the mediator sample has no response-compression middleware. | **Conditional.** Compression is useful for larger JSON but HTTP/2 or HTTP/3 does not compress response bodies, and HTTPS compression has BREACH implications. gRPC manages compression separately; streaming latency must not regress. | Do not make the old setting an unconditional Ark default. Add only after media-type, size, secret-bearing-response and streaming decisions. |
| **HSG-07 — Application Insights parity is partial and based on the classic SDK** | `Program.cs` already calls `AddApplicationInsithsTelemetryForWebHostArk`; therefore “Application Insights is absent” is no longer accurate. However, unlike `ArkStartupBase`, the sample omits `WebApiUserTelemetryInitializer` and `WebApi4xxAsSuccessTelemetryInitializer`, and it does not bridge a telemetry abstraction into SimpleInjector. The package uses `Microsoft.ApplicationInsights.AspNetCore` 2.23.0. | **Observability yes; blindly copying the old setup no.** Azure Monitor OpenTelemetry is the current direction. Marking every 4xx request successful can hide authentication, authorization and client-contract failures. | Decide classic-SDK compatibility versus OpenTelemetry migration and the user-identity/privacy model. Do not duplicate registrations already present. |
| **HSG-08 — NLog is not registered in the mediator sample host** | The ReferenceProject calls `ConfigureNLog`; the mediator `Program.cs` does not. Ark's helper is already compatible with `IHostBuilder` exposed by `WebApplicationBuilder.Host`. | **Yes for this repository's sample.** NLog is the Ark logging standard and is already installed transitively in the solution. No Minimal-API-specific NLog abstraction is needed. | Register the existing helper before build and verify that framework `ILogger` events reach NLog. |
| **HSG-09 — Main has no startup-failure log/flush boundary** | ReferenceProject `Main` catches fatal exceptions and shuts NLog down. Mediator top-level statements await `RunAsync` without a process boundary. Failures before or during `Build`, `SampleStartup.Configure`, verification or `RunAsync` have no explicit fatal NLog event or guaranteed flush. | **Yes.** Async targets can lose final events without shutdown. The ReferenceProject pattern needs one correction: swallowing the exception can produce a successful process exit. | Add an explicit async `Main` or equivalent helper, structured fatal logging with invariant culture, shutdown/flush in `finally`, and deliberate non-zero failure semantics. |
| **HSG-10 — Production entry-point wiring is not tested** | Reqnroll creates a separate `HostBuilder`, calls `SampleStartup` directly and never executes mediator `Program.cs`. NLog, Key Vault ordering, telemetry host registration and the process failure boundary can regress while transport tests stay green. | **Yes.** Composition-root tests are the smallest check for operational wiring. External telemetry and Key Vault calls must remain replaceable/inert. | Add a host-construction seam and focused tests; use a subprocess only where process exit and final flush cannot be asserted in-process. |

## Confirmed non-gaps and rejected ports

These differences should not become parity work unless a consumer requirement is
provided:

| Capability | Finding |
| --- | --- |
| Handler registration validation | Completed by GEN-03 for generated HTTP, gRPC and Rebus mappings. HSG-02 covers full container verification only. |
| Application Insights registration | Already present in mediator `Program.cs`; HSG-07 concerns the telemetry strategy and missing host/application integration. |
| ProblemDetails | The mediator sample uses the shared Ark exception handler and standard generated responses. Do not reintroduce MVC exception filters. |
| Authentication/authorization | The mediator sample has default and fallback policies and unconditional middleware. It is at least equivalent to the controller baseline. |
| OpenAPI/versioning | Native OpenAPI 3.1, generated version expansion and Scalar are deliberate replacements for Swashbuckle plus `Asp.Versioning`. |
| OData and MVC filters/model binders | Controller-specific and outside a generated Minimal API host profile. |
| Permissive CORS from `ArkStartupWebApiCommon` | `AllowCredentials`, every origin, every method/header and exposed `*` is not a safe default to port. CORS must be application opt-in with named origins. |
| Static files and request localization | Application concerns, not API host defaults. The sample can opt in when it serves assets or localized content. |
| Custom `X-Forwarded-PathBase` middleware | Superseded by standard forwarded-prefix handling with a trust boundary; see HSG-04. |
| `CaptureStartupErrors(true)` | Useful to hosting diagnostics but not a substitute for fatal logging and a non-zero exit. Whether to expose detailed errors is environment-specific. |
| `GlobalInit.InitStatics()` | The mediator sample already registers NodaTime JSON/protobuf support and injects `IClock`. Process-wide culture mutation and ReferenceProject-specific Dapper setup should not be copied. |

### ASP.NET Core subpackage disposition

| Package area | Mediator disposition |
| --- | --- |
| `Ark.Tools.AspNetCore.ApplicationInsights` | Used by the sample, with the generation and integration questions recorded as HSG-07/HST-07. |
| `Ark.Tools.AspNetCore.HealthChecks` | Operational capability is missing, but its single aggregate endpoint and Application Insights publisher are not copied automatically; see HSG-05/HST-04. |
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

- **Status:** PROPOSED
- **Recommended:** add composable service, application-builder and container
  verification extensions in a small ASP.NET Core hosting package that can be
  consumed by Minimal API hosts. Keep mediator endpoint generation independent
  from the operational host profile. Avoid inheriting from
  `ArkStartupWebApiCommon`, which would pull MVC, OData and Swashbuckle policy
  into a generated Minimal API host.
- **Alternative A:** place the profile directly in
  `Ark.Tools.MediatorFramework.MinimalApi`.
- **Alternative B:** make the sample inherit from `ArkStartupWebApi`.
- **Why review is required:** this fixes package direction and public API shape;
  it may require moving existing security defaults without breaking controller
  consumers.
- **Blocked tasks:** HST-01, HST-02, HST-03.

### HSD-02 — Container registration hooks and verification boundary

- **Status:** PROPOSED
- **Recommended:** expose two deliberate phases equivalent to the useful parts
  of `RegisterContainer()` and `RegisterContainer(IServiceProvider)`, then
  synchronously complete `Container.Verify()` after `UseSimpleInjector` and all
  generated endpoint mappings but before `RunAsync`. Verification is idempotent
  per host startup and never owned by a bus `IHostedService`.
- **Alternative:** retain hosted-service verification but extract it from the
  Rebus service and make it mandatory for the host profile.
- **Questions:** must verification include registrations that intentionally
  instantiate external clients, and should `VerificationOption.VerifyOnly`
  exclusions be permitted by host configuration?
- **Blocked tasks:** HST-01, HST-04.

### HSD-03 — Security policy ownership

- **Status:** PROPOSED
- **Recommended:** security headers default on; HSTS explicitly selected as
  `Application`, `Edge`, or `Disabled`. `Application` emits HSTS outside
  Development, `Edge` documents and tests that the application does not emit it.
  Give Scalar, Swagger UI and gRPC reflection explicit named policy treatment
  rather than path suffix guesses.
- **Alternative:** always copy `ArkStartupBase` behavior.
- **Questions:** which deployed ingress owns HSTS today, and which browser-based
  documentation UIs must support cross-origin opener behavior?
- **Blocked tasks:** HST-02.

### HSD-04 — Trusted proxy contract

- **Status:** PROPOSED
- **Recommended:** support standard forwarded headers, including
  `X-Forwarded-Prefix`, only when known proxies/networks are configured. No
  trust-all or custom-header default.
- **Questions:** list the Azure/App Service/Kubernetes ingress topologies and
  whether the edge removes caller-supplied forwarding headers.
- **Blocked tasks:** HST-03.

### HSD-05 — Health endpoint contract

- **Status:** PROPOSED
- **Recommended:** anonymous, minimal `/health/live` with no dependency checks;
  restricted `/health/ready` with tagged required dependencies. Do not expose
  exception details or the HealthChecks UI response publicly.
- **Alternative:** preserve the ReferenceProject `/healthCheck` contract for
  deployment compatibility.
- **Questions:** which orchestrator paths and response formats are already
  configured, and must Application Insights health publishing remain?
- **Blocked tasks:** HST-04.

### HSD-06 — Compression policy

- **Status:** PROPOSED
- **Recommended:** exclude compression from the default host profile until a
  benchmark identifies useful response types and a security review identifies
  secret-bearing responses. Never apply HTTP response compression to gRPC and
  prove streaming first-item latency is unchanged.
- **Alternative:** copy the existing Brotli/Gzip HTTPS setup.
- **Blocked tasks:** HST-05.

### HSD-07 — Telemetry generation

- **Status:** PROPOSED
- **Recommended:** treat the current classic Application Insights registration
  as compatibility behavior and design an Azure Monitor OpenTelemetry migration
  task before expanding it. Do not port `WebApi4xxAsSuccessTelemetryInitializer`
  by default. Record which authenticated-user claim, if any, may leave the
  process and bridge a transport-neutral telemetry abstraction to
  SimpleInjector rather than `TelemetryClient`.
- **Alternative:** complete classic-SDK parity now and defer OpenTelemetry.
- **Questions:** are existing dashboards, Snapshot Debugger, custom processors
  or NLog Application Insights targets release requirements?
- **Blocked tasks:** HST-07.

### HSD-08 — Fatal exit semantics

- **Status:** PROPOSED
- **Recommended:** log once at `Fatal`, attempt console fallback, flush/shut down
  NLog in `finally`, and preserve a non-zero exit by rethrowing after logging.
  Avoid duplicate fatal events between `Main` and
  `AppDomain.UnhandledException`.
- **Alternative:** return an explicit non-zero exit code.
- **Blocked tasks:** HST-06.

### HSD-09 — Sample intent

- **Status:** PROPOSED
- **Recommended:** keep the mediator sample runnable without Azure or SQL while
  making its in-process host operationally credible. Advanced deployment
  switches remain opt-in and documented; no production secret or live external
  call is required by tests.
- **Alternative:** keep the sample intentionally minimal and move the complete
  host example to a second sample.
- **Blocked tasks:** HST-04, HST-06, HST-07, HST-08.

## Detailed implementation tasks

### HST-01 — Composable Minimal API startup and SimpleInjector verification

**Scope:** FRAMEWORK + SAMPLE · **Blocked by:** HSD-01, HSD-02

#### Problem

HSG-01 and HSG-02 leave operational composition in one sample and make complete
verification depend on Rebus.

#### Steps

1. Introduce the approved small hosting surface with explicit hooks for
   container-only registration and MS-DI cross-wiring.
2. Define and document middleware and endpoint-mapping phases. Preserve the
   required order for exception handling, routing, authentication,
   authorization, SimpleInjector and generated endpoints.
3. Add one explicit full-container verification phase after all registrations
   can be observed. Keep existing generated missing-handler diagnostics.
4. Remove verification from `SampleBusHostedService`; leave it responsible only
   for bus start/stop and container disposal as decided.
5. Add framework tests for a missing non-handler dependency, a cross-wired MS DI
   dependency, an invalid decorator and a valid host. Assert failure before the
   server accepts a request.
6. Migrate `SampleStartup` to the approved surface without importing MVC/OData
   behavior.

#### Outcomes

- Minimal API hosts have one documented Ark composition contract.
- Full container errors fail deterministically before serving traffic.
- Web startup does not depend on the presence or order of a Rebus hosted service.

#### Acceptance

- [ ] The registration hooks run once and in documented order.
- [ ] An invalid full composition fails host startup with an actionable
  SimpleInjector diagnostic.
- [ ] Existing generated handler-registration diagnostics remain intact.
- [ ] A host without Rebus receives the same verification guarantee.
- [ ] Public APIs have XML documentation and the mediator design/guide is updated.
- [ ] Full solution build and tests pass with zero warnings.

### HST-02 — Ark security headers and HSTS host profile

**Scope:** FRAMEWORK + SAMPLE · **Blocked by:** HSD-01, HSD-03

#### Problem

HSG-03 leaves the generated Minimal API sample without the security baseline used
by Ark controller hosts, while deployment ownership of HSTS and documentation UI
exceptions is undefined.

#### Steps

1. Put the approved security policy in the hosting package selected by HSD-01;
   reuse the already-managed NetEscapades dependency only if that package
   boundary is accepted.
2. Define named API, Scalar/Swagger and gRPC-reflection policies. Document every
   relaxed header and why it is needed.
3. Implement the selected `Application`/`Edge`/`Disabled` HSTS behavior with
   environment-aware defaults.
4. Apply the profile early enough to cover normal responses, ProblemDetails,
   documentation and not-found responses.
5. Add TestServer assertions for exact headers on API success/error, Scalar,
   OpenAPI and gRPC-related HTTP responses, plus Development and edge-owned HSTS
   cases.
6. Document edge responsibilities and rollback behavior before enabling the
   profile in the sample.

#### Outcomes

- Ark Minimal API hosts can select an explicit, tested browser security profile.
- HSTS has one declared owner per deployment.

#### Acceptance

- [ ] API and documentation policy snapshots are approved.
- [ ] The `Server` header is absent where the application can control it.
- [ ] HSTS behavior matches all three ownership modes.
- [ ] No documentation or gRPC tooling regression is introduced.
- [ ] Full solution build and tests pass with zero warnings.

### HST-03 — Trusted proxy and path-base handling

**Scope:** FRAMEWORK + SAMPLE · **Blocked by:** HSD-01, HSD-04

#### Problem

HSG-04 is required for prefixed ingress deployments, but the legacy Ark custom
header must not be copied without a trust boundary.

#### Steps

1. Add opt-in forwarded-header configuration for the exact headers and trusted
   proxy/network sources approved in HSD-04.
2. Use standard forwarded-prefix behavior to establish `PathBase`; reject or
   ignore untrusted forwarding values.
3. Verify generated OpenAPI server paths, Scalar links, redirects and endpoint
   routing under a prefix.
4. Add tests for a trusted proxy, an untrusted direct caller, multiple forwarding
   hops and malformed prefixes.
5. Publish ingress examples for the approved Azure and Kubernetes topologies.

#### Outcomes

- Prefixed deployments generate correct links without trusting arbitrary client
  headers.

#### Acceptance

- [ ] Untrusted forwarding headers cannot alter scheme, host, client IP or base
  path.
- [ ] Trusted prefix routing and documentation links work end to end.
- [ ] No custom `X-Forwarded-PathBase` default is introduced.
- [ ] Full solution build and tests pass with zero warnings.

### HST-04 — Health checks and startup readiness

**Scope:** FRAMEWORK + SAMPLE · **Blocked by:** HSD-02, HSD-05, HSD-09

#### Problem

HSG-05 provides no orchestrator contract and the existing aggregate Ark endpoint
does not distinguish process liveness from dependency readiness.

#### Steps

1. Use built-in ASP.NET Core health checks for the approved live/ready paths and
   response shape.
2. Tag only mandatory request-path dependencies as readiness checks. Container
   verification remains a startup failure, not a health check.
3. Map endpoint authorization and short-circuit behavior explicitly.
4. Add sample checks for the SQL-backed mode and Rebus only if the deployment
   cannot serve its declared API contract without them.
5. Test pre-start, ready, dependency-failed and disclosure-safe response cases.
6. Document orchestrator configuration and whether the existing Application
   Insights publisher is retained.

#### Outcomes

- Deployments can distinguish a dead process from a live process that is not
  ready to receive traffic.

#### Acceptance

- [ ] Live and ready semantics, paths and authorization match HSD-05.
- [ ] Responses disclose no secrets, connection strings or exception details.
- [ ] Verification failures still prevent startup rather than reporting only
  unhealthy.
- [ ] Full solution build and tests pass with zero warnings.

### HST-05 — Evidence-based response compression

**Scope:** FRAMEWORK + SAMPLE · **Blocked by:** HSD-06

#### Problem

HSG-06 is a performance possibility, not a safe parity default.

#### Steps

1. Benchmark representative JSON, ProblemDetails, MessagePack and streaming
   responses below and above proposed size thresholds.
2. Threat-model responses that combine attacker-controlled input with secrets or
   authorization-derived data.
3. If evidence supports application compression, add an opt-in policy limited to
   approved media types and sizes; otherwise record no implementation.
4. Prove gRPC remains independently configured and first-item streaming latency
   and cancellation do not regress.

#### Outcomes

- Compression is either a measured, bounded option or an explicitly rejected
  parity item.

#### Acceptance

- [ ] Benchmark and security evidence is attached to the implementation PR.
- [ ] HTTPS compression is not enabled for secret-bearing responses.
- [ ] Streaming and gRPC behavior is unchanged.
- [ ] Full solution build and tests pass with zero warnings if code changes.

### HST-06 — NLog startup failure and shutdown boundary

**Scope:** SAMPLE · **Blocked by:** HSD-08, HSD-09

#### Problem

HSG-08 and HSG-09 mean the sample does not demonstrate the repository logging
standard and can lose startup/shutdown diagnostics.

#### Steps

1. Convert the top-level entry point to the smallest testable async-main shape
   and register the existing `ConfigureNLog` helper before host build.
2. Wrap container construction, builder configuration, Key Vault registration,
   build, startup configuration, verification and `RunAsync` in the approved
   process boundary.
3. Emit structured NLog startup-fatal and shutdown events with
   `CultureInfo.InvariantCulture`; never interpolate log messages.
4. Flush/shut down NLog in `finally` and implement the approved non-zero failure
   behavior without double logging.
5. Add focused tests using an in-memory NLog target or isolated process to prove
   a forced startup failure is logged, flushed and reported as failure.
6. Update sample configuration and run documentation without adding credentials
   or requiring an external logging target.

#### Outcomes

- The sample demonstrates Ark NLog registration and preserves startup failures
  through process termination.

#### Acceptance

- [ ] `Microsoft.Extensions.Logging` uses NLog in the production entry point.
- [ ] A failure before and after `Build` produces one structured fatal event.
- [ ] Buffered events are flushed during normal and exceptional shutdown.
- [ ] Startup failure cannot result in exit code zero.
- [ ] Full solution build and tests pass with zero warnings.

### HST-07 — Decide and implement the telemetry baseline

**Scope:** FRAMEWORK + SAMPLE · **Blocked by:** HSD-07, HSD-09

#### Problem

HSG-07 is partial classic-SDK integration, not an absence of Application
Insights. Expanding it without a generation decision would deepen migration
cost.

#### Steps

1. Inventory classic-only requirements: NLog target, custom telemetry
   processors, Snapshot Collector, request `Success` overrides, authenticated
   user fields and dashboards.
2. Prototype Azure Monitor OpenTelemetry against those requirements and record
   supported, replaced and blocked behavior.
3. Implement the approved compatibility or migration path once, avoiding the
   current host/sample duplicate-registration risk.
4. Define a transport-neutral application telemetry abstraction only if handlers
   need custom events/metrics; bridge that abstraction into SimpleInjector.
5. Apply privacy filtering and explicit claim selection before exporting user
   identity. Do not mark all 4xx responses successful.
6. Add tests with an in-memory exporter/channel proving requests, failures,
   dependencies and approved identity fields without sending telemetry.
7. Update `SMP-06` acceptance status and sample documentation to reflect the
   actual implementation.

#### Outcomes

- The repository has one intentional telemetry generation and the sample proves
  it without external transmission.

#### Acceptance

- [ ] HSD-07 requirements matrix is resolved.
- [ ] No duplicate request/dependency telemetry is emitted.
- [ ] Expected 4xx classification is explicit by status/use case.
- [ ] No unapproved claim or personal data is exported.
- [ ] The sample remains inert without telemetry configuration.
- [ ] Full solution build and tests pass with zero warnings.

### HST-08 — Production composition-root tests

**Scope:** SAMPLE · **Blocked by:** HSD-09 · **Depends on:** HST-01, HST-06,
HST-07

#### Problem

HSG-10 leaves operational `Program` wiring outside the current in-process
transport test host.

#### Steps

1. Extract only the host-construction seam needed to execute production
   registration from tests; do not create a second startup implementation.
2. Make credentials and external providers replaceable so tests never contact
   Key Vault, Azure Monitor or production NLog targets.
3. Assert production registration for NLog, telemetry, security policy,
   SimpleInjector verification and health endpoints.
4. Add one process-level failure test if HST-06 exit/flush semantics cannot be
   proven in-process.
5. Keep existing Reqnroll transport scenarios on the same `SampleStartup`
   composition.

#### Outcomes

- Production entry-point and test-host wiring cannot silently diverge.

#### Acceptance

- [ ] Tests execute the production composition root without external services.
- [ ] Duplicate or missing operational registrations fail a runnable check.
- [ ] Existing HTTP, gRPC and Rebus scenarios remain unchanged and green.
- [ ] Full solution build and tests pass with zero warnings.

## Suggested execution order after decisions

1. Resolve HSD-01 through HSD-09.
2. HST-01.
3. HST-02 and HST-03.
4. HST-04 and HST-06.
5. HST-07.
6. HST-08.
7. HST-05 only if HSD-06 requests implementation rather than documented
   deferral.
