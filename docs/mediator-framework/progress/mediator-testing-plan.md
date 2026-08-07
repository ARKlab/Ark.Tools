# Mediator Framework testing redesign — implementation plan

**Status:** Approved for implementation
**Decision log:** [`mediator-testing-decisions.md`](mediator-testing-decisions.md)

## 1. Objective

Finalize the Mediator Framework test story with two explicit ownership
boundaries:

1. Framework-owned tests under
   `tests/` prove generated hosting and
   transport behavior using synthetic contracts. These tests may use
   `TestServer`, generated gRPC clients, and in-memory Rebus processors.
2. The sample application tests under
   `samples/Ark.MediatorFramework.Sample/test/`
   prove application behavior by resolving decorated contract handler
   interfaces from the SimpleInjector composition root. They do not build an
   ASP.NET host.

The sample becomes the executable example of the application testing pattern:
Reqnroll scenarios use application contracts directly, the SQL-backed profile by
default, an explicitly selected in-memory store profile, deterministic clocks
and users, and bounded waiting for asynchronous effects.

## 2. Target architecture

```text
Reqnroll scenario
    |
    v
ApplicationTestContext
    |
    +-- SimpleInjector container
    |      ApplicationComposition.Register(...)
    |      test user/clock/store adapters
    |      outbound Rebus configuration
    |
    +-- scoped contract dispatch
    |      IQueryHandler<TQuery,TResponse>
    |      IRequestHandler<TRequest,TResponse>
    |      ICommandHandler<TCommand>
    |
    +-- optional RebusProcessorComposition
           scenario-owned InMemNetwork
           generated wrappers, retry, scope, outbox

tests/Ark.Tools.MediatorFramework.Hosting.Tests
    |
    +-- synthetic contracts and handlers
    +-- generated Minimal API TestServer
    +-- generated gRPC client against exported proto
    +-- generated Rebus processor
    +-- framework-owned wire/hosting assertions
```

The application test context must call the application composition root; it
must not duplicate handler registrations or resolve stores to arrange or assert
business behavior. The framework hosting fixture must not reference the sample
application.

## 3. Test ownership

| Concern | Owner | Required assertion |
| --- | --- | --- |
| Generated route, query, body, multipart, server-set binding | Framework hosting tests | Generated host binds valid and invalid requests correctly |
| HTTP status semantics, ProblemDetails mapping, content negotiation | Framework hosting tests | Generic contracts produce the documented transport result |
| gRPC service/version/proto shape, rich errors, metadata | Framework hosting tests | Exported schema and generated client behave correctly |
| Rebus wrapper, routing, scope, retry, cancellation | Framework hosting tests | Generated message handlers execute in a real in-memory processor |
| Azure Functions trigger/binding behavior, if supported by the framework | Framework/Azure Functions tests under `tests/` | Core Tools or host fixture proves generated trigger behavior |
| Greeting create/query/update business rules | Sample application tests | Direct contract dispatch returns state or throws the domain exception |
| FluentValidation errors | Sample application tests | Direct dispatch throws `ValidationException` with field failures |
| Business violations and not-found behavior | Sample application tests | Direct dispatch throws the typed exception and preserves its payload |
| Dapper SQL, transaction, paging, audit, and row-version behavior | Sample application tests | Same contract scenarios pass in the default SQL profile and alternate in-memory profile |
| HTTP-to-Rebus application workflow | Sample application tests | Direct command publishes and the eventual state is observed |
| Application authorization decorator and user context | Sample application tests | Claims principal changes the direct dispatch outcome |
| URLs, status codes, ProblemDetails wire shape, serialization, OpenAPI | Not sample application tests | Covered only by framework hosting capability tests |

## 4. Non-goals

- Do not make application contracts reference ASP.NET Core, gRPC, Rebus
  `MessageContext`, or `HttpContext`.
- Do not add a test-only production dependency merely to wrap a handler call.
- Do not keep duplicate HTTP and gRPC behavior matrices in the sample.
- Do not assert generated wrapper internals from application tests.
- Do not add Azurite unless the sample owns an Azure Blob-backed abstraction that
  needs coverage.
- Do not remove existing framework capability tests; extend or reorganize them
  only to make hosting ownership explicit.

## 5. Execution rules for every task

Each task is one independently reviewable change. Before marking a task
complete:

1. Follow the repository's source/header/XML-documentation and analyzer rules.
2. Use existing centrally managed packages; if a package reference changes,
   update `Directory.Packages.props` and
   every affected `packages.lock.json` with
   `dotnet restore Ark.Tools.slnx --force-evaluate`.
3. Run
   `dotnet build Ark.Tools.slnx --configuration Debug`.
4. Run
   `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`.
5. Run the focused test project as well, using `-f net10.0` for hosting tests.
6. Do not mark a task complete if the default SQL profile, the explicitly
   selected in-memory profile, or the full-solution gate fails. When SQL Server
   is unavailable, record the blocked SQL run rather than silently changing the
   default profile.

## 6. Task files and progress

Each implementation task has its own file. The tracker below is the progress
source of truth for this testing redesign; check a task only after its
acceptance criteria and required scenarios pass. Tasks in the same order group
may proceed in parallel once their dependencies are complete.

| Order | Task | Depends on | Status | Task file |
| --- | --- | --- | --- | --- |
| 1 | TST-01 — Approve ownership and update the delivery map | Decision log approval | [x] Complete | [TST-01](tasks/testing/TST-01-ownership-delivery-map.md) |
| 2 | TST-02 — Create framework-owned hosting test projects | TST-01 | [x] Complete | [TST-02](tasks/testing/TST-02-hosting-test-projects.md) |
| 3 | TST-03 — Prove generated Minimal API hosting | TST-02 | [x] Complete | [TST-03](tasks/testing/TST-03-minimal-api-hosting.md) |
| 3 | TST-04 — Prove generated gRPC hosting | TST-02 | [x] Complete | [TST-04](tasks/testing/TST-04-grpc-hosting.md) |
| 3 | TST-05 — Prove generated Rebus hosting | TST-02 | [x] Complete | [TST-05](tasks/testing/TST-05-rebus-hosting.md) |
| 3 | TST-06 — Keep other framework hosts under `tests/` | TST-02 | [x] Complete | [TST-06](tasks/testing/TST-06-other-framework-hosts.md) |
| 4 | APP-01 — Expose a direct application composition test seam | D1, D4, D7 | [ ] Planned | [APP-01](tasks/testing/APP-01-application-test-seam.md) |
| 5 | APP-02 — Rewrite Reqnroll lifecycle and dispatch steps | APP-01 | [ ] Planned | [APP-02](tasks/testing/APP-02-reqnroll-dispatch.md) |
| 6 | APP-03 — Cover synchronous application behavior | APP-02 | [ ] Planned | [APP-03](tasks/testing/APP-03-synchronous-application-behavior.md) |
| 7 | APP-04 — Exercise asynchronous workflows through in-memory Rebus | APP-01, APP-02 | [ ] Planned | [APP-04](tasks/testing/APP-04-rebus-application-workflows.md) |
| 8 | APP-05 — Run the application suite against SQL and in-memory stores | APP-03, APP-04 | [ ] Planned | [APP-05](tasks/testing/APP-05-sql-and-inmemory-stores.md) |
| 9 | APP-06 — Remove obsolete application boundary tests and dependencies | TST-03, TST-04, TST-05, APP-03, APP-05 | [ ] Planned | [APP-06](tasks/testing/APP-06-remove-boundary-tests.md) |
| 10 | DOC-01 — Publish the revised testing guidance | TST-03, TST-04, TST-05, APP-06 | [ ] Planned | [DOC-01](tasks/testing/DOC-01-testing-guidance.md) |

## 7. Completion definition

The redesign is complete only when:

- Framework hosting behavior is covered under `tests/` with synthetic
  contracts, generated clients/wrappers, and no sample dependency.
- The sample Reqnroll suite resolves only decorated application contract
  handler interfaces from a scenario-owned SimpleInjector composition.
- Validation, business violations, not-found, authorization, SQL/Dapper,
  auditing, concurrency/opaque ETags, paging, attachments, streaming,
  cancellation, and asynchronous Rebus effects are covered.
- Default tests use the local SQL implementation; the documented
  `ARK_SAMPLE_INMEMORY_TESTS=1` profile demonstrates the same persistence-
  sensitive scenarios with in-memory stores.
- Background waits are bounded, diagnose stranded work, and clean up all
  resources.
- Application tests contain none of the explicitly excluded transport
  assertions.
- The design guide, testing guide, sample README, task board, lock files (if
  project references changed), and solution project graph agree.
- The full-solution build and test gates pass with zero warnings.
