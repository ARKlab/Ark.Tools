# TST-06 — Keep other framework hosts under `tests/`

**Depends on:** TST-02
**Scope:** Existing framework host packages

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

1. Audit the current Azure Functions and any future hosting tests for references
   to sample test fixtures.
2. Move generic trigger/binding/auth/ProblemDetails/Rebus composition tests to
   `tests/` or extend the existing Azure Functions test project there.
3. Use the existing Azure Functions design/task decisions for Core Tools
   process-level tests; do not duplicate Minimal API behavior in the sample.
4. Keep application-specific handler behavior out of the framework fixtures.
5. Record any intentionally sample-owned host integration in the decision log
   and isolate it from the application BDD suite.

## Outcome

- Every framework transport has an explicit test owner, and no generic host
  behavior depends on sample startup.

## Acceptance

- [x] A repository-wide search finds no framework host test that requires
  `SampleStartup`, `SampleComposition`, or a sample generated client.
- [x] Azure Functions tests follow the existing `AZF-10` ownership under
  `tests/`.
- [x] The sample-owned Azure Functions boundary test project was removed; generic
  Core Tools coverage remains in `tests/` and no sample test asserts application behavior through
  Azure Functions Core Tools (out-of-process boundary).

## Tests

- Run the affected framework host test projects.
- Run a repository search for sample project references from `tests/`.
- Required scenarios/cases:
  - generic Azure Functions trigger, binding, authorization, ProblemDetails,
    and Rebus composition tests run from `tests/`;
  - a repository search reports no framework host dependency on
    `SampleStartup`, `SampleComposition`, or a sample generated client;
  - any retained sample host test is isolated and asserts only sample-owned
    wiring.
- Run the full-solution gates.
