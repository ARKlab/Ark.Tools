# APP-10 — Scenario-scoped external mocks and application failure observation

**Depends on:** APP-04, APP-08  
**Scope:** Sample application adapters, Reqnroll bindings, and Rebus generator

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-implementation-task)
for the implementation.

## Requirements

Application services are singletons, but external-service mocks must be owned
by one scenario. Register a singleton proxy in the application container. The
proxy resolves a scenario binding holder and throws when no scenario is active;
it must never resolve the real external service during test composition.

Do not use `AsyncLocal` as the only holder for Rebus work. Rebus owns background
threads and does not guarantee the scenario async flow. The binding holder must
be explicitly attached to the scenario-owned sender and receiver composition,
or use a scenario registry keyed by the application execution context.

`IFailed<T>` handlers are application internals. The generator should discover
and register an `IHandleMessages<IFailed<T>>` implementation from the enabled
contract assembly when one exists. Tests must not replace it with a recorder.
Observe failure handling through the external adapter mock, or reread the
affected entity after the bus is idle.

## Acceptance

- [x] A singleton application service can call a scenario mock with parameter
  matching and verification.
- [x] A call outside a scenario fails as a global test failure.
- [x] The real external service is not registered in the scenario container.
- [x] Sender and Rebus receiver resolve the same scenario binding without
  depending on `AsyncLocal` flow across Rebus threads.
- [x] Generated Rebus registration discovers same-assembly `IFailed<T>` handlers.
- [x] No sample test registers or asserts an `IFailed<T>` recorder.
- [x] Failure scenarios verify external calls or reread application state.

## Tests

- Configure a Moq-backed adapter with expected arguments and verify calls.
- Run an asynchronous workflow through the in-memory Rebus processor.
- Assert the mock is scenario-isolated and unusable after disposal.
- Assert failure handling via the durable entity or external adapter outcome.
