---
name: ark-mediator-framework
description: Use when consuming Ark.Tools.MediatorFramework from NuGet to design, add, change or review a contract (query, request, command, event or message) — its fields, routes, gRPC methods, validators, business rules, authorization, versioning, enums or personal data — or its handler, participant or endpoint.
---

# Ark.Tools.MediatorFramework

Use this skill for application code that consumes Mediator Framework from NuGet.
It is not for developing the framework or its source generators.

A contract is a public or cross-process boundary. Its decisions (which fields a
caller may send, which rule fails with which error, who may call it, how it
evolves, what personal data it carries) are expensive to change after clients
ship, so they are made with the human before any code is written.

## Contract design gate

1. Read the
   [contract design checklist](https://raw.githubusercontent.com/ARKlab/Ark.Tools/master/docs/mediator-framework/contract-design-checklist.md).
   If it cannot be fetched, use the index below; it carries the defaults.
2. Draft the brief from the checklist's brief template. Answer what you can
   derive from the request, the existing contracts and code, and the guides, and
   tag each answer `derived (<source>)`. Tag everything else `ASK`. Check the
   facts the request states (types, existing fields, classification, available
   data) against the code and put any correction at the top of the brief: a
   design built on a wrong premise is wrong however complete it is.
3. Ask the `ASK` items one at a time. Each question lists the valid
   alternatives with their consequences and a recommendation with its reason,
   so the human chooses instead of designing from scratch. Purpose, lawful
   basis, retention period and DPIA trigger are always `ASK`: they are decisions
   for the product owner and the DPO, and the brief records who decided them.
   Do not infer them from code; list the alternatives and label any
   recommendation as a suggestion the DPO must confirm.
4. Present the completed brief and wait for explicit confirmation. If nobody is
   available to confirm, stop after the brief.
5. Implement. Paste the brief into the pull request description. Put decisions
   that code cannot show into XML docs (`<summary>`, `<remarks>`); do not repeat
   what an attribute already states (route, verb, version, scope).

A new contract answers every section. A change answers the sections it affects,
plus Versioning and Privacy every time. "N/A" needs a reason.

## Checklist index

1. Operation — purpose, kind (`IQuery`/`IRequest`/`ICommand`/event/message), owner; background work is a separate internal message.
2. Fields and updatability — per-field table with every field of each touched model, changed or not; labels Updatable (`Input`), Create-only (`Create : Input`), Output-only (`Output`), Context-supplied (never in the body), Identity (envelope); PUT replaces everything and absent means default; partial update is its own operation.
3. Types and representation — value objects for domain primitives; NodaTime, never `DateTime`; explicit precision, unit, currency.
4. Validation — stateless FluentValidation, 400/`InvalidArgument`; every collection and page has a maximum; unknown `EvolvableEnum` input is rejected.
5. Business rules — stateful rules in the handler; each one a named `BusinessRuleViolation` subclass documented on the contract.
6. Failure contract — every failure with Problem Details type: 400 validation and rule, 403, 404, 409 concurrency, 412 ETag.
7. Concurrency and idempotency — ETag or version field; retry-safe POST; idempotent message handlers with a deduplication key.
8. API design — `/api/v{version}/<plural-kebab-resource>/{id}/<sub-resource>`; actions as sub-paths; gRPC `Service` = plural resource, `Method` = `VerbNoun`; deviations need a reason.
9. Lists, streaming and files — paging, sort, filter and maximum page size, or `IAsyncEnumerable`; upload size, count, content types; download disposition.
10. Limits and abuse — body, page, attachment and stream limits; rate limiting in the host; bounded expensive queries.
11. Transport exposure and serialization — only transports with a named consumer; source-generated JSON; stable protobuf numbers; MCP description and destructiveness.
12. Authorization — `Ark.Tools.Authorization` policy on the contract; instance checks; 403 by default, 404 when 403 discloses an unguessable id (GUID); list scoping; separate output models for restricted fields.
13. Messaging — publisher and every subscriber; sender, consumer, queue; retry, dead-letter, outbox; stable logical names; intrinsic TTL on every queue, dead-letter queue and outbox.
14. Versioning and compatibility — additive first, new version only with a reason; `EvolvableEnum<T>` with `NOT_SET = 0` on public contracts except externally controlled or `[Flags]` enums; reserved wire ids.
15. Privacy (GDPR) — personal-data class per field; purpose, lawful basis, retention, DPIA always `ASK`; cross-application messages carry identifiers only; internal messages may carry classified personal data; sensitive or special-category data never in a message; Ark.Tools.Compliance or manual checks.
16. Audit and observability — audited changes persist who, when, what; telemetry and log tags never carry personal data.
17. Documentation — XML docs on contract and every property; remarks for decisions code cannot show.
18. Tests — application, transport-boundary, messaging and compatibility tests that prove each decision.

## Implementation workflow

1. Read only the next relevant guide in the
   [Mediator Framework guide](https://raw.githubusercontent.com/ARKlab/Ark.Tools/master/docs/mediator-framework/README.md).
2. Keep versioned model types separate from operation envelopes.
3. Add only the metadata for the transports the brief names. Define participant
   publisher/consumer/subscriber ownership and serializer compatibility.
4. Implement the handler, validator, named violations, Ark authorization policy,
   composition, and focused tests.
5. Build the consuming application and inspect generated output. Never commit
   generated output.
6. Review API-surface, OpenAPI, protobuf, serializer, topology, and compliance
   surface diffs before declaring the contract complete.

## Generator boundary

The NuGet package generators own endpoint bindings, transport wrappers,
participant dispatch, routing registries, generated clients, and schemas. Do
not copy or recreate generated binding classes, `.g.cs` files, endpoint methods,
gRPC/Rebus/Functions/MCP wrappers, routing switches, or generator source.
Generated output is inspection evidence only. Use a handwritten adapter only
after checking the documented escape hatches and documenting why metadata cannot
express the requirement.

## Transactional messaging

A handler that changes state and sends or publishes enlists its application
context in the outbox, completes the scope, then commits:

```csharp
using var scope = _bus.Enlist(context.OutboxContext);
await _bus.Publish(new BookPrintCompleted { BookId = id }, cancellationToken: ctk);
await scope.CompleteAsync(ctk);
await context.CommitAsync(ctk);
```

`_bus.Enlist` opens an ambient `OutboxScope`; the outbox wraps the transport,
so every bus honors it and the handler never checks whether an outbox is
configured. Whether the messages are staged, drained by a processor, or sent
directly by the development pass-through is a host decision (`UseOutbox`), not
handler code. A send outside a scope goes straight to the transport.

## Guardrails

- Authorization uses `Ark.Tools.Authorization` policies and decorators; do not
  substitute Microsoft ASP.NET authorization metadata for shared permissions.
- Preserve released protobuf numbers and native logical names.
- Keep internal background messages in the application boundary when they are not
  public API contracts.
- Do not add a Reqnroll binding for every contract. Reuse coarse scenario
  drivers and keep transport assertions in transport tests.
- Keep handlers free of ASP.NET, gRPC, Rebus context, and serialization types.

## Red flags - stop

- You are writing contract or handler code before the brief is confirmed.
- You answered purpose, lawful basis, retention or DPIA yourself.
- A public contract has a plain enum that is neither externally controlled nor `[Flags]`.
- A personal-data field has no classification, or a sensitive field is in a message.
- A cross-application event or message carries personal data.
- A `PUT` leaves absent fields untouched.
- A business rule throws a generic exception instead of a named violation.
- You chose 403 or 404 for an instance without looking at the identifier type.
- An input model carries a computed or output-only property.
- You added an event without naming its publisher and every subscriber, or a
  message without its consumer, retry, TTL and outbox behavior.
- A handler sends or publishes next to a state change without `_bus.Enlist`
  completed before the commit, or casts `IBus` to reach the outbox.
- You are writing an endpoint method, generated wrapper, or routing switch.

## Common rationalizations

| Excuse | Reality |
| --- | --- |
| "The endpoint is obvious; I'll decide the route later." | Route, version, status, and auth are part of the contract and are decided in the brief. |
| "I can infer the lawful basis from the code." | Lawful basis, purpose and retention are legal decisions; ask and record who decided. |
| "We control all the clients, so a plain enum is fine." | Clients you don't know about yet, and old deployments, break on a new value; use `EvolvableEnum<T>`. |
| "It's an internal queue, retention doesn't matter." | Queues, dead-letter queues and outboxes hold copies of the data; each needs a TTL. |
| "The generated code is small, so I can copy it." | Generated code belongs to the package version and will drift; declare metadata and inspect output instead. |
| "A background message can reuse the HTTP response contract." | Separate immediate caller semantics from retryable, durable work. |
