# APP-02 — Rewrite Reqnroll lifecycle and dispatch steps

**Depends on:** APP-01
**Scope:** Sample Reqnroll hooks and bindings

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

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

## Outcome

- The sample's main BDD suite is an application-contract suite and is readable
  without knowing a transport route or wire format.

## Acceptance

- [x] No step definition in the application BDD suite references `HttpClient`,
  `HttpResponseMessage`, `GrpcChannel`, `RpcException`, a URL, status code,
  ProblemDetails, or a serializer.
- [x] Every scenario dispatches through a handler interface resolved by the
  context (`Drivers/BookDriver.cs`, `Steps/BookSteps.cs:394,437`).
- [x] Validation and domain exceptions are asserted with typed exception
  checks (`BookSteps.cs:493`, `Features/Books.feature:34`).
- [x] Scenario cleanup runs after both successful and failed steps (`Hooks/SampleTestContext.cs:57-61`, `DatabaseHooks.cs`).
- [x] The generated `.feature.cs` output builds under strict analyzers (`dotnet build` succeeds with zero warnings).

## Tests

- Run Reqnroll scenarios by their feature/category filter.
- Required scenarios/cases:
  - a scenario creates a context, sets a principal, dispatches a contract, and
    observes either a response or typed exception;
  - a failed step still runs cleanup and the next scenario gets fresh
    scenario-owned resources;
  - an async stream consumes items and observes cancellation without transport
    assertions.
- Run the entire sample test project in the default SQL profile and with
  `ARK_SAMPLE_INMEMORY_TESTS=1`.
- Run the full-solution gates.
