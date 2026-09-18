# APP-08 — Replace Stores with context factories and domain services

**Depends on:** APP-03, APP-04, APP-05, APP-07  
**Scope:** Sample application, host composition, tests, and documentation

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

1. Remove `Store` interfaces, implementations, and one-operation transaction
   wrappers from the sample. Keep this concern separate from request/DTO
   composition.
2. Give handlers an application context factory and make each handler compose
   fine-grained context reads, validation, writes, locks, external calls, and
   outbox enlistment, and commit in one explicit lifecycle.
3. Keep SQL and in-memory implementations behind the same context factory
   contract; the in-memory profile must use an `InMemory...ContextFactory`.
4. Extract reusable business rules and side-effects into singleton domain
   services.
5. Keep external systems behind mockable adapters and add binding drivers for
   those adapters.
6. Replace test Store access with contract dispatch or dedicated drivers.

## Acceptance

- [x] Repository search finds no application `Store` abstraction.
- [x] Handlers visibly own transaction boundaries and lock/idempotency choices.
- [x] Handlers use the same always-enabled outbox enlistment path for SQL and
  in-memory contexts.
- [x] SQL and in-memory context factories pass the same application scenarios.
- [x] Domain services are singleton and reusable by requests and messages.
- [x] External adapters are mockable and covered by binding drivers.
- [x] No test step resolves a persistence context for business assertions.

## Tests

- Cover transaction rollback, lock/idempotency behavior, and composed
  reads/writes.
- Run the direct application suite in SQL and explicit in-memory profiles.
- Run the full solution build and test gates.
