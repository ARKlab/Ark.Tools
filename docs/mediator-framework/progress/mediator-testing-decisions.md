# Mediator Framework testing redesign — decision log

**Status:** Proposed for approval before implementation  
**Scope:** Framework hosting tests under `tests/` and direct application-contract
tests in `samples/Ark.MediatorFramework.Sample/test/`

This log records the assumptions that affect project layout, public APIs, and
test ownership. Each item has at least two implementable alternatives. The
implementation plan proceeds with the recommended option unless a reviewer
selects another option in the PR.

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

**B — Add a framework-owned hosting test project (recommended)**

- Keep
  `tests/Ark.Tools.MediatorFramework.Tests` focused on generator and runtime
  capability tests.
- Add
  `tests/Ark.Tools.MediatorFramework.Hosting.Tests` for TestServer, generated
  gRPC-client, Rebus-processor, and other host-boundary tests.
- Define synthetic contracts and handlers inside the `tests/` project graph so
  framework tests cannot accidentally pass because of sample behavior.
- Keep the sample test project free of TestServer and generated transport-client
  dependencies after the migration.

### Recommendation

Choose B. The framework owns generated hosting behavior; the sample owns
application behavior. A project reference from framework tests to the sample
would make the ownership boundary unverifiable, so the hosting fixture must use
small test contracts with test doubles.

### Clarification requested

Approve B, or explicitly request that the hosting fixtures remain in the
existing framework test project instead of a new project.

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

**B — Keep the test kit in the application test project (recommended for this
workstream)**

- Add a small `ApplicationTestContext` under
  `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/`.
- Reuse the existing `Ark.Tools.Rebus.Tests` utilities and SimpleInjector
  APIs.
- Document the composition-root pattern so an adopting application can copy
  it without taking a new dependency.
- Extract only code proven to be application-independent into a package in a
  follow-up task.

### Recommendation

Choose B initially. An application composition root owns its registrations,
storage lifecycle, claims principal, and optional emulators; a generic package
cannot construct those safely. The framework deliverable for this plan is the
separation of framework-hosting tests from application behavior tests, not a
premature public abstraction over every application composition root.

### Clarification requested

If the intended deliverable is a consumable NuGet testing package, select A
before implementation so package design, versioning, and dependency review are
added as a blocking task.

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

**B — Remove host-bound behavior tests from the sample (recommended)**

- Move generic host tests to
  `tests/Ark.Tools.MediatorFramework.Hosting.Tests`.
- Remove `SampleTestContext`, generated gRPC-client use, and transport assertions
  from application tests.
- Keep host-specific tests only when they prove a sample-owned integration that
  the framework cannot know about; isolate those tests from the application
  BDD suite and label them as host tests.

### Recommendation

Choose B. The requested application test contract is explicit: URLs, status
codes, ProblemDetails, serialization, and OpenAPI are not application
behaviors. A sample smoke test would also encourage future transport assertions
to leak back into the application suite.

### Clarification requested

If a process-start smoke test is required for release operations, select A and
define its exact one-call scope; it must not restore the old boundary-test
matrix.

## D4 — How application contracts are dispatched

### Question

Should scenarios resolve concrete handlers, resolve the framework processor, or
resolve the contract handler interface from the SimpleInjector composition root?

### Alternatives

**A — Resolve handler interfaces from the container (recommended)**

- Resolve `IRequestHandler<TRequest,TResponse>`,
  `IQueryHandler<TQuery,TResponse>`, or `ICommandHandler<TCommand>`.
- Open one `AsyncScopedLifestyle` scope per scenario or dispatch.
- Invoke the interface with the contract instance and cancellation token.
- Never resolve a concrete handler or store from a step definition.

**B — Resolve a processor/facade**

- Register and invoke `IRequestProcessor`/`IQueryProcessor`-style services.
- Let the processor perform dynamic dispatch before the handler pipeline.
- Add a separate assertion that the generated or configured processor is used.

### Recommendation

Choose A. `ApplicationComposition.Register` already registers the decorated
handler interfaces, and interface resolution verifies the application
composition root without adding a second dispatch abstraction. A processor
facade can be introduced later if the framework publishes one as a stable API.

### Clarification requested

Confirm A unless the intended framework contract is a generated processor API
that application tests must exercise.

## D5 — Background Rebus behavior

### Question

Should asynchronous scenarios call generated Rebus wrappers directly or run a
real in-memory processor?

### Alternatives

**A — Invoke the generated wrapper or handler directly**

- Resolve the generated `IHandleMessages<T>` wrapper or application handler.
- Assert the resulting state immediately.
- Avoid transport scheduling, routing, scope, retry, and outbox behavior.

**B — Run an in-memory Rebus processor (recommended)**

- Build a sender container from `ApplicationComposition` and a receiver from
  `RebusProcessorComposition`.
- Use a scenario-owned `InMemNetwork`.
- Send or publish through `IBus`.
- Wait for queue, in-process, deferred, error-queue, and outbox counts to
  reach the expected state with a bounded retry policy.
- Drain and dispose the processor after every scenario.

### Recommendation

Choose B for asynchronous workflows. Direct handler tests remain appropriate
for synchronous business rules, but only a real in-memory processor proves the
generated wrapper, routing, scope, user-context propagation, retry behavior,
and eventual effect together without a network service.

### Clarification requested

Confirm B, including whether error-queue assertions are part of the sample
demonstration or remain framework-only.

## D6 — Storage and emulator matrix

### Question

Should every application scenario require SQL Server, or should the same
contract scenarios run against a fast in-memory profile with an opt-in SQL
profile?

### Alternatives

**A — SQL Server for every scenario**

- Start or require the SQL Server Docker container.
- Deploy the DACPAC before the suite.
- Reset all tables between scenarios.
- Do not run application behavior tests without the database.

**B — In-memory by default, SQL opt-in (recommended)**

- Use `InMemoryGreetingStore` and in-memory document storage for the default
  suite.
- Use the existing SQL Server Docker compose service and DACPAC when
  `ARK_SAMPLE_SQL_TESTS=1`.
- Run the same contract-level scenarios against both profiles where the
  behavior depends on persistence.
- Use existing Rebus in-memory transport; add Azurite only if an application
  storage abstraction actually targets Azure Blob Storage.

### Recommendation

Choose B. It gives contributors a deterministic local test run while still
covering Dapper queries, transactions, row-version ETags, audit persistence, and
the SQL outbox. The plan must not add an emulator for a storage provider the
sample does not use.

### Clarification requested

Confirm B and the environment variable name, or provide a required emulator
matrix before implementation.

## D7 — Scenario isolation and parallelism

### Question

How should Reqnroll scenarios isolate SimpleInjector containers, stores, Rebus
networks, and SQL state?

### Alternatives

**A — Scenario-owned resources (recommended)**

- Create a container, clock, claims principal, store, and Rebus network per
  scenario.
- Create and dispose the receiver processor in the same scenario.
- Disable parallel execution only for the SQL profile and any shared static
  Rebus inspectors that cannot be isolated.

**B — One process-wide fixture**

- Share one container, store, network, and processor across all scenarios.
- Reset application state in hooks.
- Disable all parallel execution.

### Recommendation

Choose A. It prevents state leakage and keeps the default in-memory suite
parallelizable. SQL reset hooks must follow the ReferenceProject pattern and
use `DELETE FROM` for FK-constrained tables; if a global static utility forces
serialization, isolate that limitation and document it.

### Clarification requested

Confirm A, or require fully serialized tests for reproducibility in CI.

