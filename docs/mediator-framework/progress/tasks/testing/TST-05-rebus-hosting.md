# TST-05 — Prove generated Rebus hosting

**Depends on:** TST-02
**Scope:** Framework hosting tests

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

1. Configure a scenario-owned `InMemNetwork`, sender container, and receiver
   container with `AsyncScopedLifestyle`.
2. Register generated Rebus handlers and the existing
   `RebusScopeDecorator<>`; use the existing
   `Ark.Tools.Rebus.Tests` `InProcessMessageInspectorStep`,
   `DrainableInMemTransport`, and timeout manager utilities where applicable.
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

## Outcome

- Generated Rebus wrappers and hosting scope behavior are verified through a
  real in-memory processor rather than direct wrapper invocation.

## Acceptance

- [x] A message travels through the generated wrapper and changes test state.
- [x] Scope and user-context assertions fail if the wrapper bypasses the
  configured container.
- [x] Retry, error queue, routing, cancellation, and deferred-message cases
  have bounded tests.
- [x] No Rebus test depends on wall-clock sleeps without a bounded retry and
  diagnostic counts.
- [x] Every test leaves an empty network and no running processor.

## Tests

- Focused test classes: `RebusDispatchTests`, `RebusScopeTests`,
  `RebusRoutingTests`, `RebusRetryTests`, and `RebusCancellationTests`.
- Run the focused project repeatedly to detect leaked workers.
- Required scenarios/cases:
  - a generated message wrapper changes state with a fresh scope and
    propagated claims principal;
  - routing, subscription, no-handler, retry, error-queue, deferred-message,
    and cancellation outcomes complete within bounded waits;
  - cleanup leaves empty queues and no running processor after success and
    failure.
- Run the full-solution gates.
