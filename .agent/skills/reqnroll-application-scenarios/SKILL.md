---
name: reqnroll-application-scenarios
description: Create reusable Reqnroll application scenarios with scenario-owned drivers and bounded asynchronous workflow diagnostics.
---

# Reqnroll application scenarios

Use this skill when adding or reviewing Reqnroll features for any project.

## Workflow

1. Read the feature, bindings, hooks, drivers, and application contracts before
   adding a verb.
2. Prefer an existing coarse verb. Add a binding only when the action is
   reusable across scenarios or features.
3. Use a scenario-owned domain driver to dispatch application request, query,
   and command contracts. Keep URLs, status codes, JSON, and generated
   transport wrappers in focused transport tests.
4. Store active entities and results in the driver. Subsequent verbs should
   act on or compare that active state without repeating identifiers.
5. Use `Table` values when they improve readability: `CreateInstance<T>` for
   one DTO, `CreateSet<T>` for a collection, `CompareToInstance` for one
   result, and `CompareToSet` for result collections.
6. Run the affected feature with the project’s intended test profile. Include
   persistence and messaging infrastructure when the behavior owns it.

## Rules

- Make Gherkin describe a user or QA action, not a handler implementation.
- Keep bindings coarse and domain-neutral: create, update, retrieve, search,
  wait, and compare are reusable; do not add one verb per business case.
- Keep mutable state scenario-owned. Never share an active entity, container,
  clock, or client between scenarios.
- Let drivers own domain state and contract dispatch; bindings should only map
  Gherkin input to driver calls and assertions.
- Model remote dependencies behind mock drivers. Do not mock
  application-owned infrastructure such as databases or message buses.
- For asynchronous workflows, build independent sender and receiver
  containers. Share only intentional scenario state and the test transport;
  never share a container or scope.
- Wait with bounded polling. Timeout messages should include enough queue,
  in-process, deferred, outbox, and error diagnostics to diagnose a failure.
- Dispose receivers, drain/reset test transport, clear queued work, and reset
  scenario data after every scenario.
- Keep public binding and driver types documented and follow the project’s
  source style.

## Example

```gherkin
Scenario: Update an active entity
    Given I create an entity with
        | Name |
        | Ada  |
    When I update the current entity with
        | Name        |
        | Ada Lovelace|
    Then the current entity is
        | Name        |
        | Ada Lovelace|
```

The create binding maps input to a contract, dispatches it through the
scenario driver, and activates the response. Update and assertion bindings
use that active response instead of repeating its identifier or transport
details.
