---
name: reqnroll-table-driven-steps
description: Create reusable Reqnroll application scenarios with table-driven builders, active scenario state, and bounded asynchronous workflow diagnostics.
---

# Reqnroll table-driven application scenarios

Use this skill when adding or reviewing Reqnroll features in Ark.Tools samples.

## Workflow

1. Read the feature, bindings, hooks, and the application contracts before
   adding a verb.
2. Prefer an existing coarse verb. Add a new binding only when the action is
   reusable across features.
3. Define setup and expected values with a `Table`. Use `CreateInstance<T>` for
   one DTO, `CreateSet<T>` for a collection, `CompareToInstance` for one result,
   and `CompareToSet` for a result collection.
4. Store the result of each action as the scenario's active value (`Current`),
   then make later verbs act on or compare that value.
5. Dispatch application request, query, and command contracts through the
   scenario driver. Keep URLs, status codes, JSON, and generated wrappers in
   focused transport tests.
6. Run the affected feature under the explicit in-memory profile. Run the SQL
   profile when persistence, outbox, or transaction behavior is involved.

## Rules

- Make Gherkin describe a user or QA action, not a handler implementation.
- Keep bindings coarse and domain-neutral: create, update, retrieve, search,
  wait, and compare are reusable; do not add one verb per business case.
- Keep state scenario-owned. Never share a mutable entity, container, clock, or
  client between scenarios.
- Use a context as the driver boundary. It owns active entities, contract
  dispatch, test identity, fake time, and cleanup.
- SQL Server and Rebus are application-owned infrastructure, not external
  dependencies. Test them normally.
- Model a remote dependency behind a mock driver. Do not mock application-owned
  SQL or Rebus behavior.
- For asynchronous Rebus features, build independent sender and receiver
  containers. Share only the scenario store when in-memory and the in-memory
  network; never share a container or scope.
- Wait with bounded polling. Timeout messages must report queue, in-process,
  deferred, outbox, and error counts.
- Dispose receivers, drain/reset the in-memory network, clear SQL outbox work,
  and reset scenario data after every scenario.
- Keep public binding data types documented and follow repository source style.

## Example

```gherkin
Scenario: Update an active greeting
    Given I create a greeting with
        | Name  |
        | Ada   |
    When I update the current greeting with
        | Message       |
        | Hello, Ada!   |
    Then the current greeting is
        | Message       |
        | Hello, Ada!   |
```

The create binding converts the first table to `CreateGreetingRequest`, dispatches
it through `ApplicationTestContext`, and assigns the response to `Current`. The
update and assertion bindings operate on that active response without repeating
its identifier or transport details.

## Repository examples

- Table builders and active Book state:
  `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Steps/BookSteps.cs`
- Greeting, ETag, search, and audit verbs:
  `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Steps/GreetingSteps.cs`
- Attachment table data and byte assertions:
  `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Steps/AttachmentSteps.cs`
- Bounded Rebus waits and cleanup:
  `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Hooks/RebusScenarioContext.cs`
