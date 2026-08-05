# Mediator Framework testing redesign — implementation plan

**Status:** Proposed  
**Repository:** `/home/runner/work/Ark.Tools/Ark.Tools`  
**Decision log:** [`mediator-testing-decisions.md`](mediator-testing-decisions.md)

## 1. Objective

Finalize the Mediator Framework test story with two explicit ownership
boundaries:

1. Framework-owned tests under
   `/home/runner/work/Ark.Tools/Ark.Tools/tests/` prove generated hosting and
   transport behavior using synthetic contracts. These tests may use
   `TestServer`, generated gRPC clients, and in-memory Rebus processors.
2. The sample application tests under
   `/home/runner/work/Ark.Tools/Ark.Tools/samples/Ark.MediatorFramework.Sample/test/`
   prove application behavior by resolving decorated contract handler
   interfaces from the SimpleInjector composition root. They do not build an
   ASP.NET host.

The sample becomes the executable example of the application testing pattern:
Reqnroll scenarios use application contracts directly, local emulators or
in-memory infrastructure, deterministic clocks and users, and bounded waiting
for asynchronous effects.

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
| Dapper SQL, transaction, paging, audit, and row-version behavior | Sample application tests | Same contract scenarios pass in the opt-in SQL profile |
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
   update `/home/runner/work/Ark.Tools/Ark.Tools/Directory.Packages.props` and
   every affected `packages.lock.json` with
   `dotnet restore Ark.Tools.slnx --force-evaluate`.
3. Run
   `dotnet build Ark.Tools.slnx --configuration Debug`.
4. Run
   `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`.
5. Run the focused test project as well, using `-f net10.0` for hosting tests.
6. Do not mark a task complete if the default in-memory profile, the opted-in
   SQL profile (when SQL Server is available), or the full-solution gate fails.

## 6. Work breakdown

### TST-01 — Approve ownership and update the delivery map

**Depends on:** Decision log approval  
**Scope:** Documentation and progress tracking

#### Implementation details

1. Resolve D1–D7 in
   `/home/runner/work/Ark.Tools/Ark.Tools/docs/mediator-framework/progress/mediator-testing-decisions.md`.
2. Add a testing-redesign workstream to
   `/home/runner/work/Ark.Tools/Ark.Tools/docs/mediator-framework/progress/tasks/README.md`
   with links to one task document per implementation task.
3. Mark the old T9.8 boundary-test wording in
   `/home/runner/work/Ark.Tools/Ark.Tools/docs/mediator-framework/progress/tasks.md`
   as superseded by this workstream; preserve the historical acceptance text
   and link to the new plan.
4. Keep the full-solution build/test gate and locked-restore rules visible in
   the new task entries.

#### Outcome

- Reviewers can see who owns each test category and which decisions are still
  open before code moves.

#### Acceptance

- [ ] Every D1–D7 has an explicit accepted option or an owner and due decision.
- [ ] The task board has a unique ID and dependency order for every task below.
- [ ] No task claims that sample application tests must assert a URL, status,
  ProblemDetails wire body, serialization, or OpenAPI document.
- [ ] Existing progress links remain valid.

#### Tests

- Check every new relative Markdown link with a repository-wide path search.
- Run `git diff --check`.
- Run the full documentation-independent build/test gate.

### TST-02 — Create framework-owned hosting test projects

**Depends on:** TST-01  
**Scope:** `/home/runner/work/Ark.Tools/Ark.Tools/tests/`

#### Implementation details

1. Add a dedicated
   `tests/Ark.Tools.MediatorFramework.Hosting.Tests/` project targeting
   `net10.0`; add it to `/home/runner/work/Ark.Tools/Ark.Tools/Ark.Tools.slnx`.
2. Mirror the existing MSTest testing-platform hook used by
   `/home/runner/work/Ark.Tools/Ark.Tools/tests/Ark.Tools.MediatorFramework.Tests/`
   so the project discovers at least one test with the repo test command.
3. Reference only framework/runtime packages and existing Ark.Tools building
   blocks. Do not reference any project under
   `samples/Ark.MediatorFramework.Sample/`.
4. Follow the existing generated-client pattern. If MSBuild ordering requires
   it, add a small
   `tests/Ark.Tools.MediatorFramework.Hosting.GrpcClient/` support project and a
   test-contracts project; export the test proto to a deterministic build
   directory and generate the client with the existing centrally managed
   `Grpc.Tools` package.
5. Add a synthetic contract set covering one request, query, command, Rebus
   message, route/query/body parameters, server-owned property, validation
   failure, business violation, streaming result, attachment, and version
   lifetime. Keep handlers deterministic and backed by test-only state.
6. Add a fixture that creates and disposes a test `Container`, configures the
   default scoped lifestyle, registers the synthetic handlers, and builds the
   Minimal API/gRPC/Rebus host layers independently.
7. Add a test-only authenticated principal provider; do not use real identity
   providers or network credentials.

#### Outcome

- The framework has an independent host-boundary test home and a synthetic
  application that cannot hide generator or runtime defects.

#### Acceptance

- [ ] The new project is in the solution and passes the normal test command.
- [ ] No hosting test project references the sample application.
- [ ] A smoke test proves the synthetic handler can be resolved from its
  container and that the fixture disposes all host resources.
- [ ] Generated proto/client artifacts are reproducible and are not committed
  as generated `bin/` or `obj/` output.
- [ ] All public fixture helpers have XML documentation.

#### Tests

- `dotnet test tests/Ark.Tools.MediatorFramework.Hosting.Tests/ -f net10.0`.
- Run the generated-client test once from a clean `obj/` directory.
- Run the full-solution build and test gates.

### TST-03 — Prove generated Minimal API hosting

**Depends on:** TST-02  
**Scope:** Framework hosting tests only

#### Implementation details

1. Build a `TestServer` from the synthetic container and generated endpoint
   mapping, without calling sample startup code.
2. Cover explicit route, query, body, optional-value, and cancellation-token
   binding. Include a route parameter on a versioned endpoint.
3. Send a request that attempts to set a `[ServerSet]` member and assert the
   handler receives the server-owned value, not the client value.
4. Exercise success, null/not-found, configured status semantics, validation
   failure, business violation, and unexpected exception mapping through the
   framework's configured ProblemDetails path.
5. Test authentication and the transport-agnostic authorization decorator with
   an anonymous principal, an authenticated principal without the policy, and a
   principal with the policy.
6. Test JSON and MessagePack request/response negotiation with the framework
   serializer configuration. Keep these assertions in this project, not in the
   sample application tests.
7. Test generated multipart attachment binding, file-count/size/content-type
   limits, attachment download, and rejection before the handler stores data.
8. Test `IAsyncEnumerable<T>` response behavior: plain JSON array, first item
   available before producer completion, empty sequence, and cancellation
   observed by the producer.
9. Test OpenAPI generation and schema filtering here, including version
   partitioning, server-set omission, polymorphism, NodaTime, and XML
   documentation. Use snapshots only for framework-generated output.

#### Outcome

- Minimal API binding, hosting, errors, serialization, OpenAPI, attachments,
  streaming, authorization, and cancellation have framework-owned behavioral
  coverage.

#### Acceptance

- [ ] Tests use only synthetic contracts and framework registrations.
- [ ] Each listed binding and error case has a named test with a deterministic
  assertion.
- [ ] Streaming tests prove incremental delivery and cancellation, not merely
  the final array.
- [ ] OpenAPI and wire assertions do not appear in the sample Reqnroll suite.
- [ ] Tests pass with no external HTTP service.

#### Tests

- Focused test classes: `MinimalApiBindingTests`, `MinimalApiErrorsTests`,
  `MinimalApiAuthorizationTests`, `MinimalApiSerializationTests`,
  `MinimalApiAttachmentsTests`, `MinimalApiStreamingTests`, and
  `MinimalApiOpenApiTests`.
- Generator snapshot tests remain in
  `tests/Ark.Tools.MediatorFramework.Tests/`; hosting tests invoke the generated
  registration to prove it works at runtime.
- Run the focused project, then the full-solution gates.

### TST-04 — Prove generated gRPC hosting

**Depends on:** TST-02  
**Scope:** Framework hosting tests and generated client support

#### Implementation details

1. Export the synthetic gRPC service proto during the build and generate a
   client with `Grpc.Tools`, following the existing sample client project
   without referencing sample proto files.
2. Host the generated service in an in-process gRPC server and call it through
   the generated client and `Grpc.Net.Client`.
3. Cover unary request/response binding, route/version lifetime as represented
   by generated services, NodaTime and polymorphic message fields, metadata
   authentication, and user-context propagation.
4. Cover `ValidationException`, business violation, not-found, authorization,
   and concurrency failures through the rich `google.rpc.Status` details owned
   by the framework.
5. Cover server streaming incrementally, cancellation, empty streams, and
   client-streaming attachment uploads including metadata-first validation.
6. Cover opaque ETag/concurrency metadata where the framework owns the mapping;
   persistence-specific row-version behavior remains in the sample SQL tests.
7. Assert exported proto text and generated client shape in framework tests, not
   in application tests.

#### Outcome

- gRPC host and wire behavior is tested independently of the sample, including
  generated clients, rich errors, streaming, uploads, authentication, and
  cancellation.

#### Acceptance

- [ ] The client is generated from the build-exported synthetic proto.
- [ ] No test constructs the code-first service contract as a substitute for the
  generated client.
- [ ] Rich error tests inspect the documented status/detail fields rather than
  internal exception strings.
- [ ] Streaming and upload tests prove incremental/cancellation behavior.
- [ ] Proto export and generated client builds are deterministic from a clean
  checkout.

#### Tests

- Focused test classes: `GrpcUnaryTests`, `GrpcErrorsTests`,
  `GrpcAuthorizationTests`, `GrpcStreamingTests`, `GrpcUploadTests`, and
  `GrpcProtoExportTests`.
- Run the client project before the hosting test project when invoking tests
  directly.
- Run the full-solution gates.

### TST-05 — Prove generated Rebus hosting

**Depends on:** TST-02  
**Scope:** Framework hosting tests

#### Implementation details

1. Configure a scenario-owned `InMemNetwork`, sender container, and receiver
   container with `AsyncScopedLifestyle`.
2. Register generated Rebus handlers and the existing
   `RebusScopeDecorator<>`; use the existing `Ark.Tools.Rebus.Tests`
   `InProcessMessageInspectorStep`, `DrainableInMemTransport`, and timeout
   manager utilities where applicable.
3. Send a synthetic `[RebusMessage]` through `IBus` and assert the handler
   receives a fresh scope, cancellation token, and propagated claims principal.
4. Assert generated owner routing, subscriptions, no-handler behavior, retry
   exhaustion, error queue headers, and deferred messages.
5. Add an outbox fixture where the framework package owns the integration; use
   a fake outbox context if persistence is not a framework responsibility.
6. Add a bounded `WaitUntilIdleAsync` helper for this project. It must report
   queue, in-process, deferred, outbox, and error counts when it times out.
7. Drain and dispose the bus, network, timeout manager, and containers in
   `finally`/after-scenario cleanup.

#### Outcome

- Generated Rebus wrappers and hosting scope behavior are verified through a
  real in-memory processor rather than direct wrapper invocation.

#### Acceptance

- [ ] A message travels through the generated wrapper and changes test state.
- [ ] Scope and user-context assertions fail if the wrapper bypasses the
  configured container.
- [ ] Retry, error queue, routing, cancellation, and deferred-message cases
  have bounded tests.
- [ ] No Rebus test depends on wall-clock sleeps without a bounded retry and
  diagnostic counts.
- [ ] Every test leaves an empty network and no running processor.

#### Tests

- Focused test classes: `RebusDispatchTests`, `RebusScopeTests`,
  `RebusRoutingTests`, `RebusRetryTests`, and `RebusCancellationTests`.
- Run the focused project repeatedly to detect leaked workers.
- Run the full-solution gates.

### TST-06 — Keep other framework hosts under `tests/`

**Depends on:** TST-02  
**Scope:** Existing framework host packages

#### Implementation details

1. Audit the current Azure Functions and any future hosting tests for references
   to sample test fixtures.
2. Move generic trigger/binding/auth/ProblemDetails/Rebus composition tests to
   `tests/` or extend the existing Azure Functions test project there.
3. Use the existing Azure Functions design/task decisions for Core Tools
   process-level tests; do not duplicate Minimal API behavior in the sample.
4. Keep application-specific handler behavior out of the framework fixtures.
5. Record any intentionally sample-owned host integration in the decision log
   and isolate it from the application BDD suite.

#### Outcome

- Every framework transport has an explicit test owner, and no generic host
  behavior depends on sample startup.

#### Acceptance

- [ ] A repository-wide search finds no framework host test that requires
  `SampleStartup`, `SampleComposition`, or a sample generated client.
- [ ] Azure Functions tests follow the existing `AZF-10` ownership under
  `tests/`.
- [ ] Any retained sample host test is documented as sample-owned and does not
  assert application behavior through a transport.

#### Tests

- Run the affected framework host test projects.
- Run a repository search for sample project references from `tests/`.
- Run the full-solution gates.

### APP-01 — Expose a direct application composition test seam

**Depends on:** D1, D4, D7  
**Scope:** Sample application and sample test infrastructure

#### Implementation details

1. Keep `/home/runner/work/Ark.Tools/Ark.Tools/samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/ApplicationComposition.cs`
   as the only registration source for handlers, validators, stores, clocks,
   auditing, validation decorators, authorization-independent decorators, and
   concurrency decorators.
2. Add or refactor a test-only `ApplicationTestContext` under
   `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/`
   that:
   - creates a scenario-owned `Container` with `AsyncScopedLifestyle`;
   - calls `ApplicationComposition.Register` rather than repeating its
     registrations;
   - registers the deterministic `FakeClock`, test claims-principal provider,
     authorization services/decorators, and a scenario-owned store;
   - configures outbound Rebus using the scenario-owned network;
   - exposes `DispatchAsync` overloads that resolve handler interfaces inside a
     scope and always use `async`/`await`;
   - exposes the container only to infrastructure bindings, never to feature
     steps for concrete store access.
3. Keep `SampleComposition` and `SampleStartup` unchanged as production host
   composition until direct tests cover the application graph; then remove only
   registrations proven to be duplicated or transport-specific.
4. Verify the container before the first dispatch and fail with the aggregated
   verification error.
5. Make the context dispose outbound Rebus and all scoped resources even when a
   scenario assertion fails.

#### Outcome

- Sample application tests can execute the same decorated handlers as
  production without an ASP.NET host or generated transport client.

#### Acceptance

- [ ] The test context has no `IHost`, `TestServer`, `HttpClient`, gRPC channel,
  generated client, URL, or serializer dependency.
- [ ] A test resolving a handler interface proves validation/audit/concurrency
  decorators are present.
- [ ] A step cannot obtain `IGreetingStore` as an application assertion API.
- [ ] Container verification and disposal are deterministic.
- [ ] All public test helper types and members have XML documentation where
  repository analyzers require it.

#### Tests

- Add a focused MSTest check for direct create/query dispatch and a failed
  validation dispatch.
- Add a test that intentionally disposes a context after an exception.
- Run the sample test project in its default in-memory profile.
- Run the full-solution gates.

### APP-02 — Rewrite Reqnroll lifecycle and dispatch steps

**Depends on:** APP-01  
**Scope:** Sample Reqnroll hooks and bindings

#### Implementation details

1. Replace the host construction in
   `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Hooks/SampleTestContext.cs`
   with the direct `ApplicationTestContext`. Keep SQL deployment/reset hooks,
   but remove TestServer and gRPC handler creation.
2. Add `BeforeScenario`/`AfterScenario` hooks that create one context,
   deterministic clock, principal, store, and Rebus network per scenario.
3. Add explicit steps for:
   - setting the authenticated subject and required policy claims;
   - dispatching request/query/command contracts;
   - capturing a response or exception without swallowing it;
   - consuming an async stream with a supplied cancellation token;
   - reading persisted state through a public query contract, not a store.
4. Rewrite
   `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Features/Greetings.feature`
   and `Steps/GreetingSteps.cs` so phrases say “send/create/query the contract”
   rather than “over HTTP” or “over gRPC”.
5. Use `AwesomeAssertions` exception assertions. Assert exception type,
   validation failures, violation properties, and business state; never assert
   HTTP status or serialized JSON.
6. Keep Reqnroll bindings narrowly scoped by feature tag so a low-context model
   can map each step to one behavior.

#### Outcome

- The sample's main BDD suite is an application-contract suite and is readable
  without knowing a transport route or wire format.

#### Acceptance

- [ ] No step definition in the application BDD suite references `HttpClient`,
  `HttpResponseMessage`, `GrpcChannel`, `RpcException`, a URL, status code,
  ProblemDetails, or a serializer.
- [ ] Every scenario dispatches through a handler interface resolved by the
  context.
- [ ] Validation and domain exceptions are asserted with typed exception
  checks.
- [ ] Scenario cleanup runs after both successful and failed steps.
- [ ] The generated `.feature.cs` output builds under strict analyzers.

#### Tests

- Run Reqnroll scenarios by their feature/category filter.
- Run the entire sample test project with no SQL environment configured.
- Run the full-solution gates.

### APP-03 — Cover synchronous application behavior

**Depends on:** APP-02  
**Scope:** Sample application Reqnroll features and focused tests

#### Implementation details

Create or rewrite features so every application handler registered by
`ApplicationComposition.Register` has a contract-level behavior or an explicit
documented reason for exclusion. The initial coverage matrix is:

| Application behavior | Contract(s) to dispatch | Assertions |
| --- | --- | --- |
| Create and read | `CreateGreetingRequest`, `GetGreetingQuery` | Returned identity/message and query result |
| Update and missing entity | `UpdateGreetingMessageRequest`, `UpdateGreetingRequest`, `GetGreetingQuery` | Updated state; typed not-found exception |
| Versioned application result | `GetGreetingV2Query` | V2 response-only fields, not a v2 URL |
| FluentValidation | All validator-backed requests/queries | `ValidationException` field failures |
| Business rule | Duplicate/create violation path | `BusinessRuleViolationException` and violation payload |
| Paging/search | `SearchGreetingsQuery`, `GetAuditsQuery` | Valid pages, total counts, stable ordering, invalid query exceptions |
| Auditing | Create/update/query through the decorated handler | User, operation, entity, identifier, deterministic timestamp |
| Authorization | Policy-decorated commands/requests | Allowed principal succeeds; missing claim throws the authorization exception |
| Polymorphic behavior | `DescribeShapeRequest` and shape contracts | Correct subtype/business result; no wire round-trip assertion |
| Attachments | Upload requests and `GetDocumentQuery` | Byte content, metadata, count/size validation, missing document exception |
| Streaming | `GetGreetingsStreamQuery` | Item order/count, empty result, producer observes cancellation |
| Inline command/notification | `RefreshGreetingCommand`, `GreetingCreatedNotification` | Command effect and notification side effect |
| Failure/dead-letter behavior | `FailingRebusRequest` where application-owned | Typed failure and eventual error outcome when dispatched through Rebus |

Use the real application decorators and public contracts. Arrange state only
through earlier contract dispatches or the documented test adapter; do not call
`SampleDataContext` or `IGreetingStore` from a step.

#### Outcome

- All application code paths, including exceptional paths, have readable
  contract-level scenarios.

#### Acceptance

- [ ] The coverage table maps every current application handler to a scenario or
  an explicit follow-up task.
- [ ] Business violations, validation failures, not-found, authorization, and
  cancellation are tested by throws/observations rather than transport errors.
- [ ] Paging, SQL-independent business rules, attachment behavior, streaming,
  and auditing are covered without serialization.
- [ ] No scenario relies on `DateTime.UtcNow`, random sleeps, or shared mutable
  state.

#### Tests

- Run each Reqnroll feature independently and as a complete suite.
- Run focused MSTest tests for cancellation and the concurrency-fault test
  decorator.
- Run the full-solution gates.

### APP-04 — Exercise asynchronous workflows through in-memory Rebus

**Depends on:** APP-01, APP-02  
**Scope:** Sample Reqnroll hooks, processor composition, and features

#### Implementation details

1. Build a sender container from the application composition and a receiver
   using
   `samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.RebusProcessor/RebusProcessorComposition.cs`.
2. Share only the scenario-owned store and `InMemNetwork`; do not share a
   container or scoped service between sender and receiver.
3. Register the existing in-process inspector and drainable transport
   utilities. Configure one worker and the same retry policy used by the
   sample.
4. Add Reqnroll verbs:
   - `When I wait for the background bus to be idle and the outbox to be empty`;
   - `When I wait for the background bus to be idle and the outbox to be empty ignoring scheduled messages`;
   - `Then the background message is eventually visible through <query contract>`;
   - `Then the error queue contains the failed message` when the scenario is
     explicitly testing failure.
5. Implement bounded polling over queue, in-process, deferred, error, and
   outbox counts. Include those counts in timeout failures.
6. Drain and clear the network, timeout manager, and outbox after each
   scenario. Do not use `TRUNCATE TABLE` for SQL tables with foreign keys.
7. Add scenarios for the synchronous part of `ComposeGreetingRequest`, the
   eventual Rebus effect, propagated user context, retry success, and retry
   exhaustion.

#### Outcome

- The sample demonstrates realistic asynchronous application testing without
  starting ASP.NET Core or contacting a broker.

#### Acceptance

- [ ] The compose scenario dispatches an application contract directly, waits
  with a bounded idle/outbox verb, and observes the effect through a query
  contract.
- [ ] A test fails if the generated wrapper does not run in the receiver
  container or if user context is lost.
- [ ] Retry/error behavior is deterministic and diagnostics identify stranded
  messages.
- [ ] No Rebus worker, timer, network, or outbox remains after a scenario.

#### Tests

- Run the asynchronous feature repeatedly to expose races.
- Run with in-memory transport both with and without the SQL outbox profile.
- Run the full-solution gates.

### APP-05 — Run the application suite against SQL and local emulators

**Depends on:** APP-03, APP-04  
**Scope:** Sample test hooks, Docker documentation, persistence behavior

#### Implementation details

1. Preserve the existing `ARK_SAMPLE_SQL_TESTS=1` opt-in and
   `ARK_SAMPLE_SQL_CONNECTION` override in the direct test context.
2. Deploy the existing DACPAC once before SQL scenarios using
   `Microsoft.SqlServer.Dac`; reset the database before every scenario with
   `[ops].[ResetFull_OnlyForTesting]`.
3. Keep the reset procedure's FK-safe order: use `DELETE FROM` for
   FK-constrained application tables and truncate only independent history
   tables.
4. Run the persistence-sensitive contract scenarios against both
   `InMemoryGreetingStore` and `SqlGreetingStore`: create/read/update,
   paging/search, audits, opaque row-version ETags, transactions, and SQL
   outbox effects.
5. Keep Rebus in-memory for the default and SQL profiles. If an application
   storage abstraction is added for Azure Blob, add a separate Azurite-tagged
   profile and run the same attachment contracts against it; otherwise document
   that the sample uses `DocumentStore` in memory and does not need Azurite.
6. Serialize only the SQL profile if shared database state requires it; keep
   scenario-owned in-memory profiles parallel where the Rebus test utilities
   permit it.
7. Update
   `samples/Ark.MediatorFramework.Sample/README.md` and its Docker instructions
   with the exact environment variables, opt-in command, and cleanup behavior.

#### Outcome

- The sample proves application persistence against the real local SQL
  implementation while retaining a fast default test run.

#### Acceptance

- [ ] Default tests pass without Docker or SQL Server.
- [ ] SQL-tagged tests deploy and reset the DACPAC and pass when SQL Server is
  available.
- [ ] Dapper query paths, transaction/outbox paths, audit persistence, paging,
  and row-version-to-opaque-ETag conversion have direct assertions.
- [ ] SQL cleanup is FK-safe and leaves no scenario data.
- [ ] Emulator documentation never embeds credentials or tokens.

#### Tests

- Run the default sample test project.
- Start `samples/Ark.MediatorFramework.Sample/docker-compose.yml`, set the SQL
  environment variables, and run the SQL-tagged tests.
- Run the full-solution gates after stopping the container.

### APP-06 — Remove obsolete application boundary tests and dependencies

**Depends on:** TST-03, TST-04, TST-05, APP-03, APP-05  
**Scope:** Sample tests and solution project graph

#### Implementation details

1. Delete or move the old host-bound `SampleTestContext` implementation,
   transport `GreetingSteps`, generated-client references, and any test that
   exists only to assert URLs, status codes, ProblemDetails, serialization, or
   OpenAPI.
2. Convert tests that contain application behavior mixed with transport setup:
   - `ConcurrencyRoundtripTests` becomes direct update/ETag/concurrency
     contract scenarios.
   - `AsyncEnumerableStreamingTests` becomes direct enumeration/cancellation
     behavior; wire streaming remains in TST-03/TST-04.
   - `FileDownloadTests` becomes direct attachment storage/retrieval behavior;
     multipart/download wire behavior remains in TST-03/TST-04.
   - `PagingTests` becomes direct query validation and result behavior; binding
     shape remains in framework tests.
   - `AuthorizationTests` keeps direct policy/user-context behavior; bearer
     parsing and HTTP/gRPC status behavior moves to framework tests.
3. Remove unused `Microsoft.AspNetCore.TestHost`, `Grpc.Net.Client`,
   generated-client, and host-only package/project references from the sample
   application test project. Retain a package only if another sample-owned
   integration test still needs it and document that owner.
4. Keep Azure Functions sample tests only for sample-owned wiring if D3 selects
   that exception; otherwise move generic trigger behavior to TST-06.
5. Verify no production application file was changed to accommodate a test
   transport.

#### Outcome

- The sample test project is a focused application behavior example, not a
  second framework hosting suite.

#### Acceptance

- [ ] A repository search finds no HTTP/gRPC client or `TestServer` in the
  direct application BDD suite.
- [ ] The removed dependencies are absent unless a documented sample-owned test
  still requires them.
- [ ] Every deleted assertion is represented by a framework-owned test or is
  explicitly listed as intentionally out of scope.
- [ ] The sample still demonstrates every application behavior in APP-03 and
  APP-04.

#### Tests

- Run `dotnet list samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Ark.MediatorFramework.Sample.Tests.csproj package`
  and inspect the remaining test dependencies.
- Run the sample test project and the framework hosting projects.
- Run the full-solution gates.

### DOC-01 — Publish the revised testing guidance

**Depends on:** TST-03, TST-04, TST-05, APP-06  
**Scope:** Mediator Framework reference and sample documentation

#### Implementation details

1. Rewrite
   `/home/runner/work/Ark.Tools/Ark.Tools/docs/mediator-framework/guide/testing.md`
   around two workflows:
   - framework maintainers use `tests/Ark.Tools.MediatorFramework.Tests` and
     the hosting test project for generated host/wire behavior;
   - application teams use their composition root, a scenario-owned
     SimpleInjector scope, direct contract dispatch, local emulators, and
     Rebus idle/outbox waiting.
2. Add a “what not to assert” section explicitly excluding URLs, status codes,
   ProblemDetails format, serialization, and OpenAPI from application tests.
3. Add a complete direct-dispatch example using the sample's
   `ApplicationComposition.Register` pattern, with no transport types.
4. Document the SQL profile, Docker startup, DACPAC reset, default in-memory
   profile, deterministic clock/user setup, and bounded background-bus wait.
5. Update the testing section of
   `/home/runner/work/Ark.Tools/Ark.Tools/docs/mediator-framework/design.md`
   so it no longer says sample scenarios exercise HTTP/gRPC public interfaces.
6. Update
   `docs/mediator-framework/guide/README.md`,
   `samples/Ark.MediatorFramework.Sample/README.md`, and the progress task
   board with the new ownership and source map.
7. Replace references to the old `SampleTestContext` boundary workflow with
   the direct context and the framework hosting test project.

#### Outcome

- A new contributor can reproduce both testing layers from repository
  documentation without inferring hidden host setup.

#### Acceptance

- [ ] All documentation uses the same ownership terms as this plan.
- [ ] Every code path in the direct-dispatch example exists in compiled sample
  code or is explicitly marked pseudocode-free guidance.
- [ ] The guide documents failure assertions, SQL/emulator setup, Rebus waits,
  cleanup, and cancellation.
- [ ] Broken links and stale references to HTTP/gRPC sample BDD steps are
  removed.

#### Tests

- Repository-wide search for `SampleTestContext`, transport wording in the
  application testing section, and stale T9.8 claims.
- Validate Markdown links by checking each target path.
- Run `git diff --check`.
- Run the full-solution build/test gates; documentation itself needs no
  separate compiler.

## 7. Completion definition

The redesign is complete only when:

- Framework hosting behavior is covered under `tests/` with synthetic
  contracts, generated clients/wrappers, and no sample dependency.
- The sample Reqnroll suite resolves only decorated application contract
  handler interfaces from a scenario-owned SimpleInjector composition.
- Validation, business violations, not-found, authorization, SQL/Dapper,
  auditing, concurrency/opaque ETags, paging, attachments, streaming,
  cancellation, and asynchronous Rebus effects are covered.
- Default tests use in-memory infrastructure; the documented SQL profile runs
  the persistence-sensitive scenarios against the local SQL emulator.
- Background waits are bounded, diagnose stranded work, and clean up all
  resources.
- Application tests contain none of the explicitly excluded transport
  assertions.
- The design guide, testing guide, sample README, task board, lock files (if
  project references changed), and solution project graph agree.
- The full-solution build and test gates pass with zero warnings.

