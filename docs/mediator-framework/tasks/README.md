# Mediator Framework — Pre-Release Task Board

Each task document is self-contained: a less-context model must be able to execute it and open a
dedicated PR from the document alone. Every task defines **Outcomes** (what exists after the PR) and
**Acceptance** (verifiable criteria; the PR is not mergeable until all pass).

Source analysis and decisions: [`../pre-release-review.md`](../pre-release-review.md).

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
| [SMP-04](sample-parity/SMP-04-optimistic-concurrency.md) | Optimistic concurrency + ETag (G6) | sample-parity |
| [SMP-05](sample-parity/SMP-05-paging.md) | Paging (G7) | sample-parity |
| [SMP-06](sample-parity/SMP-06-misc-parity.md) | App Insights, config layering, IClock, test infra (G9) | sample-parity |
| [NET-01](aspnetcore/NET-01-openapi-xml-docs.md) | XML-docs into OpenAPI + 3.1 verification (N3) | aspnetcore |

## Non-blocking (do before release if capacity allows)

| Task | Title | Category |
|---|---|---|
| [GEN-01](generator-dx/GEN-01-incremental-generators.md) | Make generators truly incremental (A1) | generator-dx |
| [GEN-02](generator-dx/GEN-02-diagnostics-for-silent-failures.md) | Diagnostics for silent generator failures (A2/B2/B3) | generator-dx |
| [GEN-03](generator-dx/GEN-03-startup-handler-verification.md) | Startup handler-registration verification (B4) | generator-dx |
| [GEN-05](generator-dx/GEN-05-rebus-cancellation-token.md) | Flow `CancellationToken` through Rebus wrappers (A10) | generator-dx |
| [GEN-06](generator-dx/GEN-06-grpc-user-context-interceptor.md) | gRPC user-context interceptor (A5) | generator-dx |

## Post-release

| Task | Title | Category |
|---|---|---|
| [NET-02](aspnetcore/NET-02-openapi-operation-transformers.md) | Per-endpoint OpenAPI operation transformers (N4) | aspnetcore |
| [NET-03](aspnetcore/NET-03-json-patch.md) | PATCH support via System.Text.Json JSON Patch (N7) | aspnetcore |
| [NET-04](aspnetcore/NET-04-auth-metrics.md) | Auth/Identity metrics in the sample (N8) | aspnetcore |
| [NET-05](aspnetcore/NET-05-sse-transport-spike.md) | SSE transport spike (N5) | aspnetcore |

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
   [ ] [GEN-07](generator-dx/GEN-07-automatic-proto-export.md)
   [ ] [GEN-08](generator-dx/GEN-08-from-assembly-api-names.md)
   [ ] [SMP-04](sample-parity/SMP-04-optimistic-concurrency.md)
   [ ] [SMP-05](sample-parity/SMP-05-paging.md)
   [ ] [SMP-06](sample-parity/SMP-06-misc-parity.md)
   [ ] [NET-01](aspnetcore/NET-01-openapi-xml-docs.md)
7. Post-release:
   [ ] [NET-02](aspnetcore/NET-02-openapi-operation-transformers.md)
   [ ] [NET-03](aspnetcore/NET-03-json-patch.md)
   [ ] [NET-04](aspnetcore/NET-04-auth-metrics.md)
   [ ] [NET-05](aspnetcore/NET-05-sse-transport-spike.md)
