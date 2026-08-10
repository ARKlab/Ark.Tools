# Mediator Framework testing redesign — decision log

**Status:** Approved for implementation
**Scope:** Framework hosting tests under `tests/` and direct application-contract
tests in `samples/Ark.MediatorFramework.Sample/test/`

This log records the assumptions that affect project layout, public APIs, and
test ownership. Each item has at least two implementable alternatives. The
implementation plan follows the accepted option for each decision below.

## D1 — Where framework hosting tests live

### Question

Should generated Minimal API, gRPC, Rebus, and other hosting behavior be tested
by extending the sample test project or by adding a framework-owned hosting test
project?

### Alternatives

**A — Extend the sample test project**

- Keep `Ark.MediatorFramework.Sample.Tests` as one project.
- Add synthetic contracts and framework-only host fixtures beside the sample
  scenarios.
- The project would reference both the application and framework code.

**B — Add a framework-owned hosting test project (accepted)**

- Keep
  `tests/Ark.Tools.MediatorFramework.Tests` focused on generator and runtime
  capability tests.
- Add
  `tests/Ark.Tools.MediatorFramework.Hosting.Tests` for TestServer, generated
  gRPC-client, Rebus-processor, and other host-boundary tests.
- Define synthetic contracts and handlers inside the `tests/` project graph so
  framework tests cannot accidentally pass because of sample behavior.
- Generate at least one test client from the build-exported synthetic `.proto`
  files and use that client for the corresponding hosting assertion.
- Keep the sample test project free of TestServer and generated transport-client
  dependencies after the migration.

### Accepted decision

Option B is accepted. The framework owns generated hosting behavior; the sample
owns application behavior. A project reference from framework tests to the
sample would make the ownership boundary unverifiable, so the hosting fixture
must use small test contracts with test doubles. The generated-client test must
consume the exported proto rather than a code-first service contract.

### Decision

B is the approved project layout and hosting-test boundary.

## D2 — Public testing package versus a sample test kit

### Question

Does “Testing Framework” mean a new NuGet package for application test
utilities, or a repository-level test architecture demonstrated by the sample?

### Alternatives

**A — Ship `Ark.Tools.MediatorFramework.Testing`**

- Provide generic container-scope and contract-dispatch helpers.
- Provide reusable in-memory Rebus waiting and cleanup helpers.
- Add package validation, XML documentation, multi-targeting, lock files, and
  a public API compatibility commitment.
- Do not put Reqnroll types in the package; applications compose their own
  bindings around the helpers.

**B — Keep the test kit in the application test project (accepted for this
workstream)**

- Add a small `ApplicationTestContext` under
  `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/`.
- Reuse the existing `Ark.Tools.Rebus.Tests` utilities and SimpleInjector
  APIs.
- Document the composition-root pattern so an adopting application can copy
  it without taking a new dependency.
- Extract only code proven to be application-independent into a package in a
  follow-up task.

### Accepted decision

Option B is accepted initially. An application composition root owns its
registrations, storage lifecycle, claims principal, and optional emulators; a
generic package cannot construct those safely. The framework deliverable for
this plan is the separation of framework-hosting tests from application
behavior tests, not a premature public abstraction over every application
composition root.

### Decision

No testing NuGet package is part of this workstream. Extract application-
independent helpers only in a follow-up task with its own package review.

## D3 — Remaining sample boundary tests

### Question

Should the sample retain a small HTTP/gRPC smoke-test subset after its
application tests move to direct contract dispatch?

### Alternatives

**A — Retain one smoke scenario per transport**

- Keep a minimal `TestServer` fixture and one authenticated HTTP and gRPC call.
- Assert only that the sample process starts and a generated endpoint is
  reachable.
- Framework tests still own detailed binding, status, serialization, and error
  coverage.

**B — Remove host-bound behavior tests from the sample (accepted)**

- Move generic host tests to
  `tests/Ark.Tools.MediatorFramework.Hosting.Tests`.
- Remove `SampleTestContext`, generated gRPC-client use, and transport assertions
  from application tests.
- Keep host-specific tests only when they prove a sample-owned integration that
  the framework cannot know about; isolate those tests from the application
  BDD suite and label them as host tests.

### Accepted decision

Option B is accepted. The requested application test contract is explicit:
URLs, status codes, ProblemDetails, serialization, and OpenAPI are not
application behaviors. A sample smoke test would also encourage future
transport assertions to leak back into the application suite.

### Decision

The sample application suite has no generic HTTP/gRPC smoke-test matrix. Any
sample-owned host integration must be isolated and documented separately.

## D4 — How application contracts are dispatched

### Question

Should scenarios resolve concrete handlers, resolve the framework processor, or
resolve the contract handler interface from the SimpleInjector composition root?

### Alternatives

**A — Resolve handler interfaces from the container (accepted)**

- Resolve `IRequestHandler<TRequest,TResponse>`,
  `IQueryHandler<TQuery,TResponse>`, or `ICommandHandler<TCommand>`.
- Open exactly one `AsyncScopedLifestyle` scope for each top-level Contract
  dispatch.
- Invoke the interface with the contract instance and cancellation token.
- Never resolve a concrete handler or store from a step definition.
- Expose a dispatch helper that owns this scope: sequential top-level Contract
  calls get separate scopes, while a handler calling another Contract internally
  continues in the current scope.

**B — Resolve a processor/facade**

- Register and invoke `IRequestProcessor`/`IQueryProcessor`-style services.
- Let the processor perform dynamic dispatch before the handler pipeline.
- Add a separate assertion that the generated or configured processor is used.

### Accepted decision

Option A is accepted. `ApplicationComposition.Register` already registers the
decorated handler interfaces, and interface resolution verifies the application
composition root without adding a second dispatch abstraction. The dispatch
helper must enforce the one-scope-per-top-level-Contract rule; a processor
facade can be introduced later if the framework publishes one as a stable API.

### Decision

A generated processor API is not required for this workstream.

## D5 — Background Rebus behavior

### Question

Should asynchronous scenarios call generated Rebus wrappers directly or run a
real in-memory processor?

### Alternatives

**A — Invoke the generated wrapper or handler directly**

- Resolve the generated `IHandleMessages<T>` wrapper or application handler.
- Assert the resulting state immediately.
- Avoid transport scheduling, routing, scope, retry, and outbox behavior.

**B — Run an in-memory Rebus processor (accepted)**

- Build a sender container from `ApplicationComposition` and a receiver from
  `RebusProcessorComposition`.
- Use a scenario-owned `InMemNetwork`.
- Send or publish through `IBus`.
- Wait for queue, in-process, deferred, error-queue, and outbox counts to
  reach the expected state with a bounded retry policy.
- Drain and dispose the processor after every scenario.

### Accepted decision

Option B is accepted for asynchronous workflows. Direct handler tests remain
appropriate for synchronous business rules, but only a real in-memory processor
proves the generated wrapper, routing, scope, user-context propagation, retry
behavior, and eventual effect together without a network service.

### Decision

The sample demonstrates the in-memory processor, including bounded failure and
error-queue assertions where the application owns the workflow.

## D6 — Storage and emulator matrix

### Question

Should the sample default to SQL Server so Dapper and the database project are
tested, while also demonstrating an in-memory store profile?

### Alternatives

**A — SQL Server by default (accepted)**

- Start or require the SQL Server Docker container for the default suite.
- Deploy the DACPAC before the suite.
- Reset all tables between scenarios.
- Run persistence-sensitive contract scenarios against the real SQL store so
  Dapper statements, transactions, row-version ETags, auditing, and the SQL
  outbox are exercised by default.

**B — In-memory store profile (accepted demonstration)**

- Use `InMemorySampleDataContextFactory` and in-memory document storage when the sample is
  explicitly run with `ARK_SAMPLE_INMEMORY_TESTS=1`.
- Run the same contract-level scenarios against both profiles where the
  behavior depends on persistence, so the sample demonstrates both store
  implementations.
- Use existing Rebus in-memory transport; add Azurite only if an application
  storage abstraction actually targets Azure Blob Storage.

### Accepted decision

Option A is the default. The sample must validate the SQL implementation and
database project in its normal test profile. Option B remains a documented
in-memory demonstration selected with `ARK_SAMPLE_INMEMORY_TESTS=1`; it must
run the same contract scenarios without replacing the SQL default. The plan
must not add an emulator for a storage provider the sample does not use.

### Decision

The SQL profile is the default. `ARK_SAMPLE_INMEMORY_TESTS=1` selects the
alternate in-memory store profile; `ARK_SAMPLE_SQL_CONNECTION` remains the
connection-string override.

## D7 — Scenario isolation and parallelism

### Question

How should Reqnroll scenarios isolate SimpleInjector containers, stores, Rebus
networks, and SQL state?

### Alternatives

**A — Scenario-owned resources (accepted primary model)**

- Create a container, clock, claims principal, store, and Rebus network per
  scenario.
- Create and dispose the receiver processor in the same scenario.
- Disable parallel execution for the SQL profile and any shared static Rebus
  inspectors that cannot be isolated.

**B — One process-wide fixture (accepted demonstration)**

- Share one container, store, network, and processor across all scenarios.
- Reset application state in hooks before each scenario.
- Disable all parallel execution and label the fixture as a lifecycle example,
  not the default application test context.

### Accepted decision

Option A is the primary model for application scenarios. The sample must also
demonstrate option B in a focused, serialized fixture so contributors can see
the reset and cleanup requirements of a process-wide setup. SQL reset hooks
must follow the ReferenceProject pattern and use `DELETE FROM` for
FK-constrained tables; if a global static utility forces serialization,
isolate that limitation and document it.

### Decision

A is used for the main suite; B is demonstrated separately and is never
silently substituted for scenario-owned isolation.
