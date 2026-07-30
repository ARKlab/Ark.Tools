# Mediator Framework guide

This is the task-oriented entry point for teams adopting the source-generated,
MVC-free mediator framework. It keeps one transport-agnostic contract and
handler usable from Minimal API, code-first gRPC, and Rebus.

## Choose a starting point

| Need | Read |
| --- | --- |
| New service | [Getting started](getting-started.md) |
| Contract and handler design | [Contracts and handlers](contracts-and-handlers.md) |
| HTTP, gRPC, or Rebus exposure | [HTTP](http-endpoints.md), [gRPC](grpc.md), [Rebus](rebus.md) |
| Existing MVC application | [Migration from MVC](../migration-from-mvc.md) |

Use this framework when a pure application operation should be exposed on
multiple wires and generated consistently. Use `Ark.Tools.AspNetCore` MVC when
you need MVC controllers, filters, model binders, or an intentionally
controller-shaped application; both can coexist during migration.

## Feature guide

- [Versioning](versioning.md) · [Errors](errors.md) · [Validation and authorization](validation-and-authorization.md)
- [Serialization](serialization.md) · [Attachments](attachments.md) · [Streaming](streaming.md)
- [OpenAPI](openapi.md) · [API-surface snapshots](api-surface-snapshots.md) · [Testing](testing.md)
- [Escape hatches](escape-hatches.md)

Rationale and architecture: [`../design.md`](../design.md).
