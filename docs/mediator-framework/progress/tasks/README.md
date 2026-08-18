# Mediator Framework — current task board

Each task document is self-contained. Its Outcomes and Acceptance section is
authoritative; this board only records category, current status, and a link.

Azure Functions messaging tasks have two additional completion requirements:
each task must update its named guide section with the implementation context
needed by the next task, and each task must extend the existing
`Ark.MediatorFramework.Sample` plus its tests where the task affects runtime,
hosting, or transport behavior. Do not create a parallel messaging sample.
Book background activities must remain runnable through either the standalone
Rebus processor or the generated Azure Functions receiver.
AZM task numbers, including the supplemental AZM-07A provider and AZM-14A
native outbox tasks, are the recommended implementation order. Each file's
Execution map is mandatory: an implementation must produce every listed
artifact and must not defer task-owned guide/sample work to AZM-16.
Every AZM task must leave the repository runnable: the full-solution build and
test gates pass at task end. Incomplete feature coverage is fine; broken or
dispatcher-less generated code is not.

## Status legend

| Status | Meaning |
| --- | --- |
| Complete | Every acceptance checkbox in the task file is checked. |
| In progress | The task file has both checked and unchecked acceptance items. |
| Pending | The task file has no checked acceptance items. |
| Cancelled | The task was explicitly cancelled in its task file. |
| Deferred | The task is intentionally deferred by an accepted decision. |

`DOC-01` exists in both `docs/` and `testing/`; the path-qualified links below
are intentional and avoid treating the two tasks as one item.

## Release blockers

### Security

| Task | Title | Status |
| --- | --- | --- |
| [SEC-01](security/SEC-01-secure-by-default-endpoints.md) | Secure-by-default generated endpoints | Pending |
| [SEC-02](security/SEC-02-unconditional-authorization-middleware.md) | Unconditional authorization middleware | Pending |
| [SEC-03](security/SEC-03-messagepack-untrusted-data.md) | MessagePack `UntrustedData` and startup resolver check | Pending |
| [SEC-04](security/SEC-04-server-set-binding-protection.md) | `[ServerSet]` binding protection | Pending |
| [SEC-05](security/SEC-05-transport-agnostic-authorization-decorator.md) | Transport-agnostic policy authorization decorator | Pending |
| [SEC-06](security/SEC-06-multipart-hardening.md) | Multipart upload hardening | Pending |
| [SEC-07](security/SEC-07-error-serialization-hardening.md) | Error serialization hardening | Complete |
| [SEC-08](security/SEC-08-test-auth-bearer-hardening.md) | Malformed bearer to 401 in the test auth scheme | Pending |

### Framework

| Task | Title | Status |
| --- | --- | --- |
| [FW-01](framework/FW-01-icommand-support.md) | `ICommand` support across all transports | Pending |
| [FW-02](framework/FW-02-http-status-semantics.md) | HTTP status semantics via attribute customization | Pending |
| [FW-03](framework/FW-03-shared-problemdetails-package.md) | Shared ProblemDetails package | Pending |
| [FW-04](framework/FW-04-file-download.md) | File download support | Pending |
| [FW-08](framework/FW-08-etag-preconditions.md) | `[ETag]` contract attribute and `If-Match` binding | Pending |
| [FW-09](framework/FW-09-etag-response-emission.md) | `ETag` response header, 304, and gRPC error parity | Pending |

### Generator DX

| Task | Title | Status |
| --- | --- | --- |
| [GEN-04](generator-dx/GEN-04-remove-hardcoded-documents-proto.md) | Remove sample `Documents.proto` from the framework generator | Complete |
| [GEN-07](generator-dx/GEN-07-automatic-proto-export.md) | Automatic proto export without host entry-point wiring | Pending |
| [GEN-08](generator-dx/GEN-08-from-assembly-api-names.md) | Name assembly-scanning APIs explicitly | Complete |

### Sample parity

| Task | Title | Status |
| --- | --- | --- |
| [SMP-01](sample-parity/SMP-01-fluentvalidation.md) | FluentValidation decorators in the sample | Complete |
| [SMP-02](sample-parity/SMP-02-sql-dapper-outbox.md) | SQL/Dapper and transactional outbox | In progress |
| [SMP-03](sample-parity/SMP-03-persisted-auditing.md) | Persisted auditing | Complete |
| [SMP-04](sample-parity/SMP-04-optimistic-concurrency.md) | Optimistic concurrency and opaque ETag | Complete |
| [SMP-05](sample-parity/SMP-05-paging.md) | Paging | Pending |
| [SMP-06](sample-parity/SMP-06-misc-parity.md) | App Insights, configuration layering, clock, and test infrastructure | Pending |

### Book sample migration

Tasks are vertical slices. Each task must add a contract, handler wiring,
contract-level BDD coverage, and its applicable build/test gate. A task must
not stop at a contract, schema, host, or documentation change.

| Task | Title | Status |
| --- | --- | --- |
| [BOOK-01](sample-book/BOOK-01-catalog-vertical-slice.md) | Book catalog create and retrieve vertical slice | Complete |
| [BOOK-02](sample-book/BOOK-02-catalog-search-persistence.md) | Book catalog search and persistence vertical slice | Complete |
| [BOOK-03](sample-book/BOOK-03-catalog-mutations-concurrency.md) | Book catalog mutations, concurrency, and audit vertical slice | Complete |
| [BOOK-04](sample-book/BOOK-04-cover-attachment-vertical-slice.md) | Book cover upload and download vertical slice | Complete |
| [BOOK-05](sample-book/BOOK-05-printing-rebus-vertical-slice.md) | Book printing process completion vertical slice | Complete |
| [BOOK-06](sample-book/BOOK-06-reviews-reading-activity-vertical-slice.md) | Book reviews and reading activity vertical slice | Complete |
| [BOOK-07](sample-book/BOOK-07-streaming-editions-transport.md) | Book streaming, editions, and transport parity vertical slice | Complete |
| [BOOK-08](sample-book/BOOK-08-complete-book-migration.md) | Complete Book migration and Greeting removal | Complete |

### ASP.NET Core

| Task | Title | Status |
| --- | --- | --- |
| [NET-01](aspnetcore/NET-01-openapi-xml-docs.md) | OpenAPI 3.1 verification, YAML, and doc UI decision | Complete |

## Release-scope extension

| Task | Title | Status |
| --- | --- | --- |
| [NET-06](aspnetcore/NET-06-openapi-tags-operation-names.md) | OpenAPI tags and operation names from the contract | Complete |
| [GEN-09](generator-dx/GEN-09-xml-documentation.md) | XML documentation into OpenAPI and exported `.proto` | Pending |
| [FW-05](framework/FW-05-standard-problem-responses.md) | Standard 400/403/500 ProblemDetails responses | Pending |
| [FW-06](framework/FW-06-async-enumerable-streaming.md) | `IAsyncEnumerable<T>` streaming responses | Complete |
| [FW-07](framework/FW-07-multifile-uploads.md) | Multi-file uploads bound to an attachment collection | Complete |
| [GEN-10](generator-dx/GEN-10-api-surface-snapshots.md) | API-surface snapshot gate | Pending |
| [DOC-01](docs/DOC-01-user-documentation.md) | User documentation: getting started and feature guide | Pending |

## Non-blocking improvements

| Task | Title | Status |
| --- | --- | --- |
| [GEN-01](generator-dx/GEN-01-incremental-generators.md) | Make generators truly incremental | Complete |
| [GEN-02](generator-dx/GEN-02-diagnostics-for-silent-failures.md) | Diagnostics for silent generator failures | Complete |
| [GEN-03](generator-dx/GEN-03-startup-handler-verification.md) | Startup handler-registration verification | Complete |
| [GEN-05](generator-dx/GEN-05-rebus-cancellation-token.md) | Flow `CancellationToken` through Rebus wrappers | Complete |
| [GEN-06](generator-dx/GEN-06-grpc-user-context-interceptor.md) | gRPC user-context interceptor | Cancelled |
| [GEN-11](generator-dx/GEN-11-rename-http-binding-attributes.md) | Rename HTTP binding attributes | In progress |
| [GEN-12](generator-dx/GEN-12-evolvable-enums.md) | Evolve enum contracts without breaking strict clients | Complete |
| [FW-10](framework/FW-10-remove-http-auth-metadata.md) | Remove authentication and authorization metadata from HTTP contracts | Complete |
| [FW-11](framework/FW-11-configure-version-prefix-at-mapping.md) | Configure the common version route prefix at mapping time | Complete |

## Future improvements

| Task | Title | Status |
| --- | --- | --- |
| [NET-02](aspnetcore/NET-02-openapi-operation-transformers.md) | Per-endpoint OpenAPI operation transformers | Pending |
| [NET-03](aspnetcore/NET-03-json-patch.md) | PATCH support via System.Text.Json JSON Patch | Pending |
| [NET-04](aspnetcore/NET-04-auth-metrics.md) | Auth and Identity metrics in the sample | Pending |
| [NET-05](aspnetcore/NET-05-sse-transport-spike.md) | SSE transport spike | Pending |

## Minimal API hosting defaults

Decisions and evidence are in
[`../aspnetcore-hosting-gap-analysis.md`](../aspnetcore-hosting-gap-analysis.md).

| Task | Title | Status |
| --- | --- | --- |
| [HST-01](aspnetcore/HST-01-composable-minimal-api-startup.md) | Composable Minimal API startup | Complete |
| [HST-02](aspnetcore/HST-02-security-headers-hsts-profile.md) | Security headers and HSTS defaults | Complete |
| [HST-03](aspnetcore/HST-03-path-base-validation.md) | Strict forwarded-prefix handling | In progress |
| [HST-04](aspnetcore/HST-04-health-endpoint.md) | Default health endpoint | Complete |
| [HST-05](aspnetcore/HST-05-response-compression.md) | Default response compression | Complete |
| [HST-06](aspnetcore/HST-06-nlog-process-boundary.md) | NLog process boundary | Complete |
| [HST-07](aspnetcore/HST-07-classic-application-insights.md) | Complete classic Application Insights defaults | Complete |
| [HST-08](aspnetcore/HST-08-composition-root-tests.md) | Production composition-root tests | Complete |
| [HST-09](aspnetcore/HST-09-startup-error-diagnostics.md) | Startup-error diagnostics | Complete |

## Azure Functions isolated-worker hosting

The architecture and accepted decisions are in
[`../azure-functions-design.md`](../../azure-functions-design.md) and
[`../azure-functions-decision-log.md`](../azure-functions-decision-log.md).

| Task | Title | Status |
| --- | --- | --- |
| [AZF-01](azure-functions/AZF-01-foundation.md) | Package and shared HTTP model foundation | Pending |
| [AZF-02](azure-functions/AZF-02-trigger-generator.md) | Trigger generation, routing, and version expansion | Pending |
| [AZF-03](azure-functions/AZF-03-binding-dispatch.md) | JSON/route/query binding and scoped dispatch | Pending |
| [AZF-04](azure-functions/AZF-04-auth-user-context.md) | Authentication, authorization, and user context | In progress |
| [AZF-05](azure-functions/AZF-05-results-problems-etags.md) | Results, ProblemDetails, and ETags | In progress |
| [AZF-06](azure-functions/AZF-06-files-streaming.md) | Uploads, downloads, and JSON streaming | Pending |
| [AZF-07](azure-functions/AZF-07-one-way-rebus.md) | Outbound-only Rebus composition | In progress |
| [AZF-08](azure-functions/AZF-08-sample-host.md) | Mediator sample Functions host | Pending |
| [AZF-09](azure-functions/AZF-09-openapi.md) | OpenAPI | Deferred |
| [AZF-10](azure-functions/AZF-10-boundary-parity.md) | Core Tools tests, parity matrix, and guide | Pending |

## Azure Functions messaging

The design baseline is
[`../azure-functions-messaging-design.md`](../azure-functions-messaging-design.md).
These tasks extend the Functions host without replacing the existing Rebus
transport or starting a Rebus worker/outbox processor in a Function app.

| Task | Title | Status |
| --- | --- | --- |
| [AZM-01](azure-functions/AZM-01-shared-network-configuration.md) | Shared messaging network configuration and capability model | Complete |
| [AZM-02](azure-functions/AZM-02-message-contracts-and-host-metadata.md) | Transport-neutral message contracts and participant metadata | Pending |
| [AZM-03](azure-functions/AZM-03-message-contract-api-surface.md) | Message contract API-surface enforcement | Pending |
| [AZM-04](azure-functions/AZM-04-envelope-and-serialization.md) | Multi-type envelope and serialization protocols | Pending |
| [AZM-05](azure-functions/AZM-05-transport-abstraction-and-inmemory.md) | Transport abstraction and first-class InMemory transport | Pending |
| [AZM-06](azure-functions/AZM-06-pipeline-and-context-propagation.md) | Incoming/outgoing pipeline and context propagation | Pending |
| [AZM-07](azure-functions/AZM-07-compression-and-databus.md) | Compression and shared DataBus claim-check | Pending |
| [AZM-07A](azure-functions/AZM-07A-azure-blob-databus.md) | Azure Blob DataBus provider and IaC lifecycle contract | Pending |
| [AZM-08](azure-functions/AZM-08-restricted-bus.md) | Restricted `IBus` shim | Pending |
| [AZM-09](azure-functions/AZM-09-dispatch-retry-and-failure.md) | Scoped dispatch, settlement, retries, and second-level failure | Pending |
| [AZM-10](azure-functions/AZM-10-servicebus-transport-and-trigger-generation.md) | Azure Service Bus transport and trigger source generation | Pending |
| [AZM-11](azure-functions/AZM-11-storage-queue-transport.md) | Azure Storage Queue transport and trigger generation | Pending |
| [AZM-12](azure-functions/AZM-12-resource-lifecycle.md) | Concurrency-safe Service Bus resource lifecycle | Pending |
| [AZM-13](azure-functions/AZM-13-package-and-composition.md) | Functions messaging package and composition | Pending |
| [AZM-14](azure-functions/AZM-14-rebus-compatibility.md) | Rebus compatibility and generated Rebus host setup | Pending |
| [AZM-14A](azure-functions/AZM-14A-native-sql-outbox.md) | Native SQL outbox and hosted processor | Pending |
| [AZM-15](azure-functions/AZM-15-three-host-sample.md) | Three-participant publish/subscribe sample | Pending |
| [AZM-16](azure-functions/AZM-16-documentation-and-api-baseline.md) | User documentation, migration guidance, and API baseline | Pending |

## Testing redesign

The architecture and accepted decisions are in
[`../mediator-testing-plan.md`](../mediator-testing-plan.md) and
[`../mediator-testing-decisions.md`](../mediator-testing-decisions.md).

| Task | Title | Status |
| --- | --- | --- |
| [TST-01](testing/TST-01-ownership-delivery-map.md) | Approve ownership and update the delivery map | Complete |
| [TST-02](testing/TST-02-hosting-test-projects.md) | Create framework-owned hosting test projects | Complete |
| [TST-03](testing/TST-03-minimal-api-hosting.md) | Prove generated Minimal API hosting | Complete |
| [TST-04](testing/TST-04-grpc-hosting.md) | Prove generated gRPC hosting | Complete |
| [TST-05](testing/TST-05-rebus-hosting.md) | Prove generated Rebus hosting | Complete |
| [TST-06](testing/TST-06-other-framework-hosts.md) | Keep other framework hosts under `tests/` | Complete |
| [APP-01](testing/APP-01-application-test-seam.md) | Expose a direct application composition test seam | Pending |
| [APP-02](testing/APP-02-reqnroll-dispatch.md) | Rewrite Reqnroll lifecycle and dispatch steps | Pending |
| [APP-03](testing/APP-03-synchronous-application-behavior.md) | Cover synchronous application behavior | Complete |
| [APP-04](testing/APP-04-rebus-application-workflows.md) | Exercise asynchronous workflows through in-memory Rebus | Complete |
| [APP-05](testing/APP-05-sql-and-inmemory-stores.md) | Run the application suite against SQL and in-memory stores | Complete |
| [APP-06](testing/APP-06-remove-boundary-tests.md) | Remove obsolete application boundary tests and dependencies | Complete |
| [APP-07](testing/APP-07-request-dto-composition.md) | Adopt composed request and DTO contracts | Pending |
| [APP-08](testing/APP-08-context-factory-architecture.md) | Replace Stores with context factories and domain services | Pending |
| [APP-09](testing/APP-09-inmemory-outbox.md) | Keep transactional outbox parity in test profiles | Complete |
| [APP-10](testing/APP-10-scenario-scoped-external-mocks.md) | Scenario-scoped external mocks and application failure observation | Complete |
| [DOC-01](testing/DOC-01-testing-guidance.md) | Publish the revised testing and application guidance | Pending |

## Task rules

- Do not copy acceptance criteria into this board.
- Do not edit task documents to update this index.
- Run the full-solution build and test gates required by the task file before
  changing a task to Complete.
