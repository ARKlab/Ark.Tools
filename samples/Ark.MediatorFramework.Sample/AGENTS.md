# AI Agent Instructions for Ark.MediatorFramework.Sample

This sample is the executable application-pattern example for the Mediator
Framework. Follow the repository `AGENTS.md` and the framework guidance under
`docs/mediator-framework/`.

## Application architecture

- Keep contracts transport-neutral and follow
  `docs/mediator-framework/guide/request-and-dto-best-practices.md`.
- Namespace versioned models and operation contracts with static classes.
- Use `Input`/`Create`/`Update`/`Output` model inheritance and compose model
  payloads into `Request`/`Query`/`Command` envelopes.
- Handlers own transaction lifecycles, including locks, idempotency decisions,
  interleaved external calls, and commit/rollback.
- Context factories and DAL contexts expose fine-grained, composable ORM
  operations. The in-memory profile must use an in-memory context factory with
  the same context interface as SQL, including the composable in-memory outbox.
- Configure Rebus with the outbox in every profile. Handlers always enlist the
  current context's outbox; do not branch on SQL versus in-memory storage.
- Singleton domain services own reusable business logic and side-effects.
- External adapters own calls to systems outside this service. Each adapter
  must have a mock/stub implementation and a test binding driver.

## Explicitly rejected pattern

Do not add a `Store` interface or class. The Store pattern is an anti-pattern
in this sample: one Store method per operation hides the transaction boundary,
lock strategy, idempotency design, and the handler's responsibility. Do not
rename a Store while retaining the same abstraction; replace it with a
context factory and fine-grained context methods.

## Testing

- Test application behavior by dispatching decorated contracts directly.
- Arrange and assert business state through contracts or dedicated binding
  drivers, never by resolving a persistence context from a Reqnroll step.
- Keep HTTP, gRPC, OpenAPI, serialization, and generated-wrapper assertions in
  framework hosting tests.
- Use SQL by default and `ARK_SAMPLE_INMEMORY_TESTS=1` for the explicit
  in-memory context-factory profile.
- Use `AwesomeAssertions`, deterministic clocks and bounded waits. Never use
  arbitrary sleeps.

All public APIs require XML documentation. Use file-scoped namespaces,
Conventional Commits, and `async`/`await` in asynchronous methods.
