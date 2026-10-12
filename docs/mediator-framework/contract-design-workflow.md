# Contract design workflow

Use this workflow before adding a Mediator Framework contract or changing a
released contract property. A contract is a public or cross-process boundary,
not merely a C# record. Decide its ownership, transports, wire shape, behavior,
and compatibility before writing code.

## Consumer boundary

This guide is for applications consuming `Ark.Tools.MediatorFramework` from
NuGet. The package generators own endpoint bindings, transport wrappers,
participant dispatch, routing registries, and generated clients or schemas.
Application code declares contracts and handlers; it must not copy or recreate
generated bindings.

If you are changing the Mediator Framework repository itself, use the framework
design and generator tests instead. Do not apply the consumer workflow to
generator implementation work.

## Required contract brief

Do not create or modify a contract until the
[contract design checklist](contract-design-checklist.md) is answered and the
brief is confirmed. The checklist covers fields and updatability, validation,
business rules, failures, API design, authorization, versioning, privacy, and
the remaining decisions; this page explains how the answers shape the contract.

## Decision tree

```text
Need a new or changed contract?
|
|-- Is this a framework/generator repository change?
|     |-- yes -> stop; use generator design and tests, not this consumer workflow
|     `-- no -> continue as a NuGet consumer
|
|-- Is the operation read-only?
|     |-- yes -> IQuery<TResponse>; define filters, paging, sorting, and null/not-found behavior
|     `-- no
|          |-- returns a value -> IRequest<TResponse>
|          `-- no value -> ICommand
|
|-- Must work happen after the caller returns, be retried, delayed, or run elsewhere?
|     |-- yes -> separate the immediate request from an internal Message/Command;
|     |          name the sender, consumer participant, queue, retry, idempotency,
|     |          outbox, and completion event
|     `-- no -> keep one synchronous application operation
|
|-- Is an event emitted?
|     |-- yes -> identify the publisher and every subscriber; define payload ownership,
|     |          logical name, serializer agreement, delivery failure, and replay behavior
|     `-- no -> continue
|
|-- Is HTTP exposure required?
|     |-- yes -> choose verb, exact route, API version, request envelope, route/query/body
|     |          sources, success/null status, limits, and anonymous/authenticated policy
|     `-- no -> do not add HttpEndpoint metadata
|
|-- Is gRPC, MessagePack, MCP, Rebus, or native messaging exposure required?
|     |-- yes -> read only that transport guide and add its contract metadata;
|     |          keep host registration and generated output host-owned
|     `-- no -> use the smallest transport-neutral contract
|
|-- Is a property computed or returned only?
|     |-- yes -> put it on the output model, not on the input model
|     `-- no -> keep it client-controlled and validate it
|
`-- Is the proposed change compatible with deployed clients?
      |-- yes -> preserve defaults and wire identifiers; add only compatible optional data
      `-- no -> create a new version/contract, keep the old surface during migration,
               and test old and new consumers
```

## Shape the contract

Keep model types separate from operation envelopes. Use versioned
`Input`/`Create`/`Update`/`Output` models and compose them into request, query,
or command envelopes. Put route and query values on the operation envelope when
they are not reusable model data. Computed and output-only properties belong on
the `Output` model, never on the `Input` or `Create` model the request accepts.

For HTTP, route placeholders, `[HttpRoute]`, `[HttpQuery]`, and `[HttpBody]`
must be deliberate. Choose `201`, `202`, `204`, `404`, or another outcome based
on the operation semantics, not generator defaults. For multipart, decide body
size, file count, content-type, and antiforgery limits before implementation.

For gRPC, assign stable `[ProtoMember]` numbers and never reuse released
numbers. For MessagePack, use explicit contract metadata and the configured
resolver. For native messaging, logical names, participant serializer sets,
publisher/subscriber compatibility, and `FormerNames` are part of the wire
contract.

## Validation and authorization

Create a `FluentValidation` validator for input syntax, ranges, cross-property
invariants, paging limits, upload metadata, and state-independent business
preconditions. Keep persistence-dependent rules in application policies or
handlers when validation cannot decide without a transaction.

Use `PolicyAuthorizeAttribute` or an application-specific wrapper from
`Ark.Tools.Authorization` when the permission must apply across HTTP, gRPC,
Rebus, or direct dispatch. Use host route-group authorization only for
HTTP-specific policy. Do not replace Ark authorization with Microsoft ASP.NET
authorization metadata on the contract.

## Application and generated boundaries

Write:

- contract/model records and their XML documentation;
- explicit handler implementations;
- validators, Ark authorization policies, and decorators;
- participant/network declarations and host composition;
- source-generated serializer registrations;
- direct application and transport-boundary tests.

Do not write:

- generated endpoint methods or binding classes;
- generated gRPC, Rebus, Functions, MCP, or participant wrappers;
- copied generator code or generated `.g.cs` files;
- hand-written routing registries or transport dispatch switches;
- framework source changes in a NuGet consumer project.

Build the consumer and inspect generated output only to verify that package
metadata produced the expected surface. Generated output is evidence, not source
code to commit.

## Compatibility review

Before changing a released property or operation, classify the change:

- optional additive data with the existing default is usually compatible;
- required, removed, retyped, or semantically changed data is breaking;
- changing an optional default, enum behavior, authorization, error, route, or
  status is observable and requires compatibility review;
- protobuf field numbers and native logical names remain reserved after release;
- use `[Versioning]` and a replacement contract when old consumers must continue
  working.

Review the API-surface snapshot, OpenAPI, exported protobuf, serializer metadata,
and messaging topology whenever the relevant surface changes.

## Test ownership

Direct application tests prove handler behavior, validation, authorization,
transactions, idempotency, persistence, and business effects. Host-boundary
tests prove HTTP routes/statuses/auth, gRPC status and schema, OpenAPI,
serialization, Functions, and generated startup behavior. Messaging tests prove
routing, retries, outbox delivery, subscribers, and eventual effects.

Do not add Reqnroll bindings for every contract. Prefer an existing scenario
driver or a focused direct-dispatch test; add a coarse reusable binding only
when the feature requires a new user-facing action.

## References

- [Contract design checklist](contract-design-checklist.md)
- [Contracts and handlers](contracts-and-handlers.md)
- [Request and DTO best practices](request-and-dto-best-practices.md)
- [Validation and authorization](validation-and-authorization.md)
- [HTTP endpoints](http-endpoints.md)
- [Versioning](versioning.md)
- [Rebus](rebus.md)
- [Serialization](serialization.md)
- [Testing](testing.md)
- [Escape hatches](escape-hatches.md)
