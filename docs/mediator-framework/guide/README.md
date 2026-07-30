# Mediator Framework guide

This is the task-oriented entry point for teams adopting the source-generated,
MVC-free mediator framework. It keeps one transport-agnostic contract and
handler usable from Minimal API, code-first gRPC, and Rebus. Each page explains
the workflow, shows an application-level example, and states the observable
outcome.

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

| Capability | Use it when | Result |
| --- | --- | --- |
| [Versioning](versioning.md) | A released wire contract must evolve | Versioned routes, documents, and gRPC methods remain compatible |
| [Errors](errors.md) | A caller needs a safe, actionable failure | Domain failures become consistent HTTP and gRPC errors |
| [Validation and authorization](validation-and-authorization.md) | Input and permission must be enforced everywhere | Invalid or denied calls do not reach the handler |
| [Serialization](serialization.md) | Clients need JSON, MessagePack, or protobuf | Each wire format has explicit, compatible metadata |
| [Attachments](attachments.md) | An operation transfers files | Handlers receive transport-neutral streams under limits |
| [Streaming](streaming.md) | A caller consumes an incremental sequence | HTTP JSON and gRPC deliver values with cancellation |
| [OpenAPI](openapi.md) | HTTP consumers need a discoverable contract | Generated documents include versions, schemas, and security |
| [API-surface snapshots](api-surface-snapshots.md) | A public change needs explicit approval | Unreviewed wire changes fail the build |
| [Testing](testing.md) | You need confidence in public behavior | Boundary tests cover generated wiring and business outcomes |
| [Escape hatches](escape-hatches.md) | Attributes cannot express the required behavior | A small adapter preserves the shared handler |

Rationale and architecture: [`../design.md`](../design.md).
