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

The sample also defines the application layering used by those tests:
handlers own transaction lifecycles, context factories expose fine-grained
composable persistence operations, singleton domain services own reusable
business behavior, and external adapters isolate systems outside the service.
The Store pattern is explicitly rejected because it hides transaction,
idempotency, and lock decisions inside one-operation methods. Rebus outbox
composition is enabled in every sample profile; SQL and in-memory contexts
must provide the same transactional outbox seam.

## 2. Target architecture

```text
Reqnroll scenario
    |
    v
ApplicationTestContext
    |
    +-- SimpleInjector container
    |      ApplicationComposition.Register(...)
    |      test user/clock/context adapters
    |      outbound Rebus configuration and outbox
    |
    +-- scoped contract dispatch
    |      IQueryHandler<TQuery,TResponse>
    |      IRequestHandler<TRequest,TResponse>
    |      ICommandHandler<TCommand>
    |
    +-- optional RebusProcessorComposition
           scenario-owned InMemNetwork
           generated wrappers, retry, scope, always-enabled outbox

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
| Request/model composition and driver bindings | Sample application tests | Drivers keep the current model and compose operation envelopes |
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

Each implementation task remains self-contained. The current status board is
[`tasks/README.md`](tasks/README.md); it derives status from each task file's
acceptance checklist and is the only place that tracks completion. This plan
defines the testing architecture and dependency boundaries, not a duplicate
execution table.

The testing task sequence is:

1. TST-01 through TST-06 establish framework-owned hosting-test ownership.
2. APP-01 and APP-02 establish direct application composition and dispatch.
3. APP-03 through APP-06 cover synchronous behavior, Rebus workflows, store
   profiles, and removal of obsolete boundary tests.
4. APP-07 and APP-08 complete request composition and context-factory
   architecture.
5. APP-09 and APP-10 preserve outbox parity and scenario-scoped external
   mocks.
6. The path-qualified testing task
   [`testing/DOC-01`](tasks/testing/DOC-01-testing-guidance.md) documents the
   resulting architecture.

## 7. Completion definition

The redesign is complete only when:

- Framework hosting behavior is covered under `tests/` with synthetic
  contracts, generated clients/wrappers, and no sample dependency.
- The sample Reqnroll suite resolves only decorated application contract
  handler interfaces from a scenario-owned SimpleInjector composition.
- External adapters use scenario-scoped mock bindings; application-owned
  `IFailed<T>` handlers are registered by the generator when present and are
  observed through effects or reread entity state, never replaced by tests.
- All sample operation contracts use the model/request composition pattern and
  test drivers compose payloads without exposing persistence contexts.
- The sample has no Store abstraction; SQL and in-memory profiles implement the
  same fine-grained context factory contract.
- Validation, business violations, not-found, authorization, SQL/Dapper,
  auditing, concurrency/opaque ETags, paging, attachments, streaming,
  cancellation, and asynchronous Rebus effects are covered.
- Default tests use the local SQL implementation; the documented
  `ARK_SAMPLE_INMEMORY_TESTS=1` profile demonstrates the same persistence-
  sensitive scenarios with in-memory stores and the composable in-memory
  outbox.
- Background waits are bounded, diagnose stranded work, and clean up all
  resources.
- Application tests contain none of the explicitly excluded transport
  assertions.
- The design guide, testing guide, sample README, task board, lock files (if
  project references changed), and solution project graph agree.
- The full-solution build and test gates pass with zero warnings.
