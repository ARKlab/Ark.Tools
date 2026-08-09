# APP-01 — Expose a direct application composition test seam

**Depends on:** D1, D4, D7
**Scope:** Sample application and sample test infrastructure

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

1. Keep
   `samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/ApplicationComposition.cs`
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
   - exposes `DispatchAsync` overloads that resolve handler interfaces inside
     exactly one scope per top-level contract call and always use
     `async`/`await`;
   - makes sequential top-level contract calls open separate scopes while a
     handler calling another contract internally continues in the current
     scope;
   - exposes the container only to infrastructure bindings, never to feature
     steps for concrete store access.
3. Keep `SampleComposition` and `SampleStartup` unchanged as production host
   composition until direct tests cover the application graph; then remove only
   registrations proven to be duplicated or transport-specific.
4. Verify the container before the first dispatch and fail with the aggregated
   verification error.
5. Make the context dispose outbound Rebus and all scoped resources even when a
   scenario assertion fails.
6. Add a separately tagged process-wide fixture example that shares a container,
   store, network, and processor, resets state before each scenario, and
   disables parallel execution. Keep it out of the main scenario-owned
   application suite.

## Outcome

- Sample application tests can execute the same decorated handlers as
  production without an ASP.NET host or generated transport client.

## Acceptance

- [ ] The test context has no `IHost`, `TestServer`, `HttpClient`, gRPC channel,
  generated client, URL, or serializer dependency.
- [ ] A test resolving a handler interface proves validation/audit/concurrency
  decorators are present.
- [ ] A scope-lifecycle test proves sequential top-level dispatches do not reuse
  a scope, while a nested contract call from a handler does reuse the current
  scope.
- [ ] Focused lifecycle tests demonstrate both scenario-owned resources and the
  separately serialized process-wide fixture.
- [ ] A step cannot obtain a persistence context as an application assertion API.
- [ ] Container verification and disposal are deterministic.
- [ ] All public test helper types and members have XML documentation where
  repository analyzers require it.

## Tests

- Add a focused MSTest check for direct create/query dispatch and a failed
  validation dispatch.
- Required scenarios/cases:
  - dispatch two independent contracts and assert distinct scoped instances;
  - dispatch a handler that calls another contract and assert the nested call
    observes the current scope;
  - run the process-wide fixture twice and assert reset, serialization, and
    cleanup between scenarios;
  - dispose the context after a failed dispatch and verify all resources close.
- Run the sample test project in its default SQL profile and its explicit
  in-memory profile.
- Run the full-solution gates.
