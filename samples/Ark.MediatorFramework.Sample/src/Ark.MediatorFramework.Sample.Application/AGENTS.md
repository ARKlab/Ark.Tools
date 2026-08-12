# Sample Application guidance

This project demonstrates the application layer consumed by the Mediator
Framework transports.

- Handlers own transaction, lock, idempotency, outbox, and commit lifecycles.
- Contexts/DALs expose fine-grained composable ORM operations only.
- Singleton domain services contain reusable business rules and side-effects.
- Domain services publish messages and call external systems through adapters.
- External adapters have mock/stub implementations and scenario binding drivers.
- SQL, Rebus, blob storage, and the local outbox are service-owned persistence;
  they are contexts/DALs, not external adapters.
- Do not add a `Store` interface or class. The Store pattern hides transaction
  boundaries and is explicitly rejected.

Keep application-owned contracts transport-neutral. Public requests, queries,
responses, and DTOs belong in the sibling API project; Rebus-only workflow
messages belong under `Messages/`. Namespace versioned models and operation
contracts with static classes, use `Input`/`Create`/`Update`/`Output`
inheritance, and compose model payloads into request envelopes.

Keep the project organized by responsibility: `Host/` for composition,
`Handlers/` for handlers and validators, `DAL/` for persistence, and
`Services/` for decorators and application services.

Application tests dispatch contracts directly. They keep the current model in a
driver, use scenario-scoped external mocks, and observe application-owned
failure handling through external effects or reread entity state after the bus
is idle. They do not replace or mock `IFailed<T>` application internals.
