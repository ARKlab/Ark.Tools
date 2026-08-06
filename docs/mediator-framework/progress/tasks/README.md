# Mediator Framework — Pre-Release Task Board

Each task document is self-contained: a less-context model must be able to execute it and open a
dedicated PR from the document alone. Every task defines **Outcomes** (what exists after the PR) and
**Acceptance** (verifiable criteria; the PR is not mergeable until all pass).

Source analysis and decisions: [`../pre-release-review.md`](../pre-release-review.md).
Delivery-tracking index: [`../README.md`](../README.md).

## Conventions for every task PR

- Branch/PR per task; conventional-commit title (e.g. `feat(mediator): ...`, `fix(mediator): ...`).
- Build gate: `dotnet build Ark.Tools.slnx --configuration Debug` must succeed with zero warnings (TreatWarningsAsErrors).
- Test gate: `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` must pass.
- CI uses `RestoreLockedMode=true`: any dependency change must include updated `packages.lock.json` files (`dotnet restore --force-evaluate`).
- All public APIs need XML docs; NLog structured logging with `CultureInfo.InvariantCulture`; file-scoped namespaces; standard copyright header.
- Update `docs/mediator-framework/design.md` when a task changes framework behavior described there.
- Update the progress tracker below when a task is completed; check recent commits before starting the next pending task.

## Release blockers (decision D7)

| Task | Title | Category |
|---|---|---|
| [SEC-01](security/SEC-01-secure-by-default-endpoints.md) | Secure-by-default generated endpoints (attribute + route group) | security |
| [SEC-02](security/SEC-02-unconditional-authorization-middleware.md) | Unconditional authorization middleware | security |
| [SEC-03](security/SEC-03-messagepack-untrusted-data.md) | MessagePack `UntrustedData` + startup resolver check | security |
| [SEC-04](security/SEC-04-server-set-binding-protection.md) | `[ServerSet]` binding protection against mass assignment | security |
| [SEC-05](security/SEC-05-transport-agnostic-authorization-decorator.md) | Transport-agnostic policy authorization decorator | security |
| [SEC-06](security/SEC-06-multipart-hardening.md) | Multipart upload hardening | security |
| [SEC-07](security/SEC-07-error-serialization-hardening.md) | Error serialization hardening | security |
| [SEC-08](security/SEC-08-test-auth-bearer-hardening.md) | Malformed bearer → 401 in test auth scheme | security |
| [FW-01](framework/FW-01-icommand-support.md) | `ICommand` support across all transports (G1) | framework |
| [FW-02](framework/FW-02-http-status-semantics.md) | HTTP status semantics via attribute customization (G3) | framework |
| [FW-03](framework/FW-03-shared-problemdetails-package.md) | Shared ProblemDetails package (A4/D5) | framework |
| [FW-04](framework/FW-04-file-download.md) | File download support (G10) | framework |
| [GEN-04](generator-dx/GEN-04-remove-hardcoded-documents-proto.md) | Remove sample `Documents.proto` from framework generator (A6) | generator-dx |
| [GEN-07](generator-dx/GEN-07-automatic-proto-export.md) | Automatic proto export without host entry-point wiring | generator-dx |
| [GEN-08](generator-dx/GEN-08-from-assembly-api-names.md) | Name assembly-scanning APIs explicitly | generator-dx |
| [SMP-01](sample-parity/SMP-01-fluentvalidation.md) | FluentValidation decorators in sample (G2) | sample-parity |
| [SMP-02](sample-parity/SMP-02-sql-dapper-outbox.md) | SQL/Dapper + transactional Outbox (G4) | sample-parity |
| [SMP-03](sample-parity/SMP-03-persisted-auditing.md) | Persisted auditing (G5) | sample-parity |
| [FW-08](framework/FW-08-etag-preconditions.md) | `[ETag]` contract attribute + `If-Match` binding (G6) | framework |
| [FW-09](framework/FW-09-etag-response-emission.md) | `ETag` response header + 304 + gRPC error parity (G6) | framework |
| [SMP-04](sample-parity/SMP-04-optimistic-concurrency.md) | Optimistic concurrency + opaque ETag in the sample (G6) | sample-parity |
| [SMP-05](sample-parity/SMP-05-paging.md) | Paging (G7) | sample-parity |
| [SMP-06](sample-parity/SMP-06-misc-parity.md) | App Insights, config layering, IClock, test infra (G9) | sample-parity |
| [NET-01](aspnetcore/NET-01-openapi-xml-docs.md) | OpenAPI 3.1 verification, YAML, doc-UI decision (N3) | aspnetcore |

## Release blockers — 2026-07 scope extension (decision D8)

| Task | Title | Category |
|---|---|---|
| [NET-06](aspnetcore/NET-06-openapi-tags-operation-names.md) | OpenAPI tags and operation names from the contract | aspnetcore |
| [GEN-09](generator-dx/GEN-09-xml-documentation.md) | XML documentation into OpenAPI and exported `.proto` | generator-dx |
| [FW-05](framework/FW-05-standard-problem-responses.md) | Standard 400/403/500 ProblemDetails responses on every endpoint | framework |
| [FW-06](framework/FW-06-async-enumerable-streaming.md) | `IAsyncEnumerable<T>` streaming responses | framework |
| [FW-07](framework/FW-07-multifile-uploads.md) | Multi-file uploads bound to an attachment collection | framework |
| [GEN-10](generator-dx/GEN-10-api-surface-snapshots.md) | API-surface snapshot gate (contracts, routes, gRPC methods) | generator-dx |
| [DOC-01](docs/DOC-01-user-documentation.md) | User documentation: getting started + feature guide | docs |

## Non-blocking (do before release if capacity allows)

| Task | Title | Category |
|---|---|---|
| [GEN-01](generator-dx/GEN-01-incremental-generators.md) | Make generators truly incremental (A1) | generator-dx |
| [GEN-02](generator-dx/GEN-02-diagnostics-for-silent-failures.md) | Diagnostics for silent generator failures (A2/B2/B3) | generator-dx |
| [GEN-03](generator-dx/GEN-03-startup-handler-verification.md) | Startup handler-registration verification (B4) | generator-dx |
| [GEN-05](generator-dx/GEN-05-rebus-cancellation-token.md) | Flow `CancellationToken` through Rebus wrappers (A10) | generator-dx |
| [GEN-06](generator-dx/GEN-06-grpc-user-context-interceptor.md) | gRPC user-context interceptor (A5) | generator-dx |
| [GEN-11](generator-dx/GEN-11-rename-http-binding-attributes.md) | Rename HTTP binding attributes to transport-specific names | generator-dx |
| [GEN-12](generator-dx/GEN-12-evolvable-enums.md) | Evolve enum contracts without breaking strict clients | generator-dx |
| [FW-10](framework/FW-10-remove-http-auth-metadata.md) | Remove authentication and authorization metadata from HTTP contracts | framework |
| [FW-11](framework/FW-11-configure-version-prefix-at-mapping.md) | Configure the common version route prefix at mapping time | framework |

## Future improvements

| Task | Title | Category |
|---|---|---|
| [NET-02](aspnetcore/NET-02-openapi-operation-transformers.md) | Per-endpoint OpenAPI operation transformers (N4) | aspnetcore |
| [NET-03](aspnetcore/NET-03-json-patch.md) | PATCH support via System.Text.Json JSON Patch (N7) | aspnetcore |
| [NET-04](aspnetcore/NET-04-auth-metrics.md) | Auth/Identity metrics in the sample (N8) | aspnetcore |
| [NET-05](aspnetcore/NET-05-sse-transport-spike.md) | SSE transport spike (N5) | aspnetcore |

## Minimal API hosting defaults

Decisions and evidence are recorded in
[`../aspnetcore-hosting-gap-analysis.md`](../aspnetcore-hosting-gap-analysis.md).

| Task | Title |
|---|---|
| [HST-01](aspnetcore/HST-01-composable-minimal-api-startup.md) | Composable Minimal API startup |
| [HST-02](aspnetcore/HST-02-security-headers-hsts-profile.md) | Security headers and HSTS defaults |
| [HST-03](aspnetcore/HST-03-path-base-validation.md) | Strict forwarded-prefix handling |
| [HST-04](aspnetcore/HST-04-health-endpoint.md) | Default health endpoint |
| [HST-05](aspnetcore/HST-05-response-compression.md) | Default response compression |
| [HST-06](aspnetcore/HST-06-nlog-process-boundary.md) | NLog process boundary |
| [HST-07](aspnetcore/HST-07-classic-application-insights.md) | Complete classic Application Insights defaults |
| [HST-08](aspnetcore/HST-08-composition-root-tests.md) | Production composition-root tests |
| [HST-09](aspnetcore/HST-09-startup-error-diagnostics.md) | Startup-error diagnostics |

### Recommended execution order

1. [x] HST-01 → [x] HST-05.
2. [x] HST-06.
3. [x] HST-07 → [x] HST-08 → [ ] HST-09.

## Azure Functions hosting

The design is in
[`../../azure-functions-design.md`](../../azure-functions-design.md), with accepted
decisions in the
[`Azure Functions decision log`](../azure-functions-decision-log.md).

Framework tests, including Core Tools end-to-end tests that prove generated
Functions work, live under `tests/`. Projects under `samples/` showcase application
capabilities and how an application can test its own composition; they do not test
the framework libraries.

| Task | Title | Depends on |
|---|---|---|
| [AZF-01](azure-functions/AZF-01-foundation.md) | Package and shared HTTP model foundation | AZD-01, AZD-02, AZD-09, AZD-10 |
| [AZF-02](azure-functions/AZF-02-trigger-generator.md) | Trigger generation, routing and version expansion | AZF-01 |
| [AZF-03](azure-functions/AZF-03-binding-dispatch.md) | JSON/route/query binding and scoped dispatch | AZF-02 |
| [AZF-04](azure-functions/AZF-04-auth-user-context.md) | Authentication, authorization and user context | AZF-03, AZD-03, AZD-04 |
| [AZF-05](azure-functions/AZF-05-results-problems-etags.md) | Results, ProblemDetails and ETags | AZF-04 |
| [AZF-06](azure-functions/AZF-06-files-streaming.md) | Uploads, downloads and JSON streaming | AZF-05, AZD-06 |
| [AZF-07](azure-functions/AZF-07-one-way-rebus.md) | Outbound-only Rebus composition | AZF-05, AZD-08 |
| [AZF-08](azure-functions/AZF-08-sample-host.md) | Mediator sample Functions host | AZF-06, AZF-07, AZD-10 |
| [AZF-09](azure-functions/AZF-09-openapi.md) | OpenAPI (deferred by AZD-11) | — |
| [AZF-10](azure-functions/AZF-10-boundary-parity.md) | Core Tools tests, parity matrix and guide | AZF-08, AZD-10 |

### Recommended Azure Functions execution order

1. [x] Resolve AZD-01 through AZD-09 in the decision log.
2. [x] Resolve AZD-10 and AZD-11.
3. [x] AZF-01 → [x] AZF-02 → [x] AZF-03.
4. [x] AZF-04 → [x] AZF-05.
5. [x] [AZF-06](azure-functions/AZF-06-files-streaming.md) → [x] [AZF-07](azure-functions/AZF-07-one-way-rebus.md) (independent after AZF-05).
6. [x] AZF-08.
7. [x] AZF-10. AZF-09 remains deferred until AZD-11 is reopened.

Also see [`../future-improvements.md`](../future-improvements.md) (WebApplicationFactory auth substitution, AoT sample, N6, N9).

## Recommended execution order

Track completion in this order. `SEC-01` through `SEC-06` and `SEC-08` are checked based on the
recent security commits `8502585`, `fd4d600`, `938567d`, and `c0fc361`.

1. [x] [SEC-01](security/SEC-01-secure-by-default-endpoints.md)
   [x] [SEC-02](security/SEC-02-unconditional-authorization-middleware.md)
   [x] [SEC-03](security/SEC-03-messagepack-untrusted-data.md)
   [x] [SEC-04](security/SEC-04-server-set-binding-protection.md)
   [x] [GEN-04](generator-dx/GEN-04-remove-hardcoded-documents-proto.md)
2. [x] [FW-01](framework/FW-01-icommand-support.md)
   [x] [FW-02](framework/FW-02-http-status-semantics.md)
3. [x] [GEN-01](generator-dx/GEN-01-incremental-generators.md)
   [x] [GEN-02](generator-dx/GEN-02-diagnostics-for-silent-failures.md)
   [x] [GEN-03](generator-dx/GEN-03-startup-handler-verification.md)
   [x] [GEN-05](generator-dx/GEN-05-rebus-cancellation-token.md)
   [x] [GEN-06](generator-dx/GEN-06-grpc-user-context-interceptor.md) *(cancelled — existing ASP.NET Core host propagation retained)*
4. [x] [SMP-01](sample-parity/SMP-01-fluentvalidation.md)
   [x] [SEC-05](security/SEC-05-transport-agnostic-authorization-decorator.md)
5. [x] [FW-03](framework/FW-03-shared-problemdetails-package.md)
   [x] [SEC-07](security/SEC-07-error-serialization-hardening.md)
   [x] [SEC-06](security/SEC-06-multipart-hardening.md)
   [x] [SEC-08](security/SEC-08-test-auth-bearer-hardening.md)
   [x] [FW-04](framework/FW-04-file-download.md)
6. [x] [SMP-02](sample-parity/SMP-02-sql-dapper-outbox.md)
   [x] [SMP-03](sample-parity/SMP-03-persisted-auditing.md)
   [x] [GEN-07](generator-dx/GEN-07-automatic-proto-export.md)
   [x] [GEN-08](generator-dx/GEN-08-from-assembly-api-names.md)
   [x] [FW-08](framework/FW-08-etag-preconditions.md)
   [x] [FW-09](framework/FW-09-etag-response-emission.md)
   [x] [SMP-04](sample-parity/SMP-04-optimistic-concurrency.md)
   [x] [SMP-05](sample-parity/SMP-05-paging.md)
   [x] [SMP-06](sample-parity/SMP-06-misc-parity.md)
   [x] [NET-01](aspnetcore/NET-01-openapi-xml-docs.md)
7. Scope extension (D8) — wire-shape items first, documentation last:
   [x] [NET-06](aspnetcore/NET-06-openapi-tags-operation-names.md)
   [x] [FW-05](framework/FW-05-standard-problem-responses.md)
   [x] [FW-06](framework/FW-06-async-enumerable-streaming.md)
   [x] [FW-07](framework/FW-07-multifile-uploads.md)
   [x] [GEN-09](generator-dx/GEN-09-xml-documentation.md)
   [x] [GEN-10](generator-dx/GEN-10-api-surface-snapshots.md)
   [x] [DOC-01](docs/DOC-01-user-documentation.md)
8. Additional pre-release improvements:
   [x] [FW-10](framework/FW-10-remove-http-auth-metadata.md)
   [x] [FW-11](framework/FW-11-configure-version-prefix-at-mapping.md)
   [x] [GEN-11](generator-dx/GEN-11-rename-http-binding-attributes.md)
   [x] [GEN-12](generator-dx/GEN-12-evolvable-enums.md)
9. Future improvements:
   [ ] [NET-02](aspnetcore/NET-02-openapi-operation-transformers.md)
   [ ] [NET-03](aspnetcore/NET-03-json-patch.md)
   [ ] [NET-04](aspnetcore/NET-04-auth-metrics.md)
   [ ] [NET-05](aspnetcore/NET-05-sse-transport-spike.md)
