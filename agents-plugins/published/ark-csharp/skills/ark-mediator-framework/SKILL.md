---
name: ark-mediator-framework
description: Use when consuming Ark.Tools.MediatorFramework from NuGet and adding or changing a contract, request, query, command, event, message, property, endpoint, participant, validator, or authorization policy.
---

# Ark.Tools.MediatorFramework

Use this skill for application code that consumes Mediator Framework from NuGet.
Read the [contract design workflow](https://raw.githubusercontent.com/ARKlab/Ark.Tools/main/docs/mediator-framework/contract-design-workflow.md)
before editing a contract. This skill is not for developing the framework or
its source generators.

## Mandatory information gate

Before creating or modifying a contract or property, write a contract brief and
answer every applicable question:

- What business operation or fact is this, and is it a query, request, command,
  event, or background message?
- Is it exposed as HTTP? Which verb, exact route, API version, status codes,
  request binding, and anonymous/authenticated behavior?
- What is the model? Which properties are required, optional, computed,
  server-set, write-once, immutable, or concurrency-controlled?
- Is it an event? Who publishes it, who subscribes, and what delivery/replay
  behavior is required?
- Is it background work or a message? Who sends it, which participant consumes
  it, and what are retry, dead-letter, idempotency, and outbox rules?
- Which FluentValidation rules cover syntax, ranges, cross-field invariants,
  sizes, and state-independent preconditions?
- Which `Ark.Tools.Authorization` policy or scope applies? Is it shared across
  transports or HTTP-only?
- Which persistence, transaction, locking, external-call, and ownership
  boundaries apply?
- Which transports and serializers carry the contract?
- Is the change compatible? What version, route, protobuf field numbers,
  logical names, former names, OpenAPI, and API-surface changes result?
- Which direct application, transport, messaging, and generated-surface tests
  prove the decision?

Do not start implementation while an answer is assumed, guessed, or missing.

## Workflow

1. Read the decision tree and only the next relevant guide in the
   [Mediator Framework guide](https://raw.githubusercontent.com/ARKlab/Ark.Tools/main/docs/mediator-framework/README.md).
2. Decide the contract kind: `IQuery<T>`, `IRequest<T>`, or `ICommand`; split
   immediate HTTP work from delayed/retried background messages.
3. Keep versioned model types separate from operation envelopes. Mark
   server-owned values `[ServerSet]`; never use it as authorization.
4. Add only the metadata for transports actually required. Define participant
   publisher/consumer/subscriber ownership and serializer compatibility.
5. Implement the application handler, FluentValidation validator, Ark
   authorization policy/decorator, composition, and focused tests.
6. Build the consuming application and inspect generated output for correctness.
   Never commit generated output.
7. Review API-surface, OpenAPI, protobuf, serializer, topology, and compatibility
   diffs before declaring the contract complete.

## Generator boundary

The NuGet package generators own endpoint bindings, transport wrappers,
participant dispatch, routing registries, generated clients, and schemas. Do
not copy or recreate generated binding classes, `.g.cs` files, endpoint methods,
gRPC/Rebus/Functions/MCP wrappers, routing switches, or generator source.
Generated output is inspection evidence only. Use a handwritten adapter only
after checking the documented escape hatches and documenting why metadata cannot
express the requirement.

## Guardrails

- Authorization uses `Ark.Tools.Authorization` policies and decorators; do not
  substitute Microsoft ASP.NET authorization metadata for shared permissions.
- `[ServerSet]` prevents client binding but does not authenticate, authorize, or
  populate a value.
- Preserve released protobuf numbers and native logical names; use a new version
  for breaking or semantic changes.
- Keep internal background messages in the application boundary when they are not
  public API contracts.
- Do not add a Reqnroll binding for every contract. Reuse coarse scenario
  drivers and keep transport assertions in transport tests.
- Keep handlers free of ASP.NET, gRPC, Rebus context, and serialization types.

## Red flags - stop

- You are about to write an endpoint method, generated wrapper, or routing switch.
- You cannot state the exact HTTP route or have not decided that HTTP is absent.
- A property is server-owned but is not marked `[ServerSet]`.
- You changed a released property without checking compatibility and versioning.
- You added an event without naming its publisher and every subscriber.
- You added a message without naming its consumer, queue/participant, retry, and
  outbox behavior.
- You used Microsoft ASP.NET authorization for an operation policy.
- You are adding bindings because the generator output is inconvenient.

## Common rationalizations

| Excuse | Reality |
| --- | --- |
| "The endpoint is obvious; I'll decide the route later." | Route, version, status, and auth are part of the contract and must be decided first. |
| "The generated code is small, so I can copy it." | Generated code belongs to the package version and will drift; declare metadata and inspect output instead. |
| "`ServerSet` is enough security." | It blocks over-posting only; authentication and Ark authorization remain required. |
| "This event has no subscribers yet." | An event without an owner and subscriber plan has no reliable delivery contract; record the intended participants or do not publish it. |
| "A background message can reuse the HTTP response contract." | Separate immediate caller semantics from retryable, durable work. |
| "A binding makes the test easier." | Drivers and direct dispatch keep scenarios stable; bindings should describe reusable user actions, not implementation types. |
