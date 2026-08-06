# APP-04 — Exercise asynchronous workflows through in-memory Rebus

**Depends on:** APP-01, APP-02
**Scope:** Sample Reqnroll hooks, processor composition, and features

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

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

## Outcome

- The sample demonstrates realistic asynchronous application testing without
  starting ASP.NET Core or contacting a broker.

## Acceptance

- [ ] The compose scenario dispatches an application contract directly, waits
  with a bounded idle/outbox verb, and observes the effect through a query
  contract.
- [ ] A test fails if the generated wrapper does not run in the receiver
  container or if user context is lost.
- [ ] Retry/error behavior is deterministic and diagnostics identify stranded
  messages.
- [ ] No Rebus worker, timer, network, or outbox remains after a scenario.

## Tests

- Run the asynchronous feature repeatedly to expose races.
- Run with in-memory Rebus transport in both the default SQL store profile and
  the explicit in-memory store profile.
- Required scenarios/cases:
  - compose a greeting, observe the eventual query result, and preserve the
    authenticated user context;
  - cover retry success and retry exhaustion with bounded error-queue
    diagnostics;
  - verify cleanup leaves no worker, timer, network, or outbox work.
- Run the full-solution gates.
