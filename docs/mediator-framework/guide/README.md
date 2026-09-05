# Mediator Framework guide

This guide teaches the framework by extending one small operation. You start
with a `Ping` contract and a Minimal API host, then add validation,
authorization, gRPC, Rebus, streaming, OpenAPI, Azure Functions, and Reqnroll.
Each chapter explains:

1. what the application author writes;
2. what the source generator creates;
3. what the host must configure;
4. how to call and test the result;
5. which boundary belongs in an application test versus a transport test.

The working reference is the Book-focused
[`samples/Ark.MediatorFramework.Sample`](../../../samples/Ark.MediatorFramework.Sample/README.md).
It has more domain operations than the tutorial, including catalog, reviews,
reading activity, covers, streaming, editions, and Rebus printing.

## Follow the learning path

| Step | Capability | Read |
| --- | --- | --- |
| 1 | A first `Ping` request, handler, and `Program.cs` | [Getting started](getting-started.md) |
| 2 | Application/host split and SimpleInjector composition | [Host setup and composition](host-setup-and-composition.md) |
| 3 | Contract shape, DTOs, versions, and server-owned fields | [Contracts and handlers](contracts-and-handlers.md), [Request and DTO best practices](request-and-dto-best-practices.md), [Versioning](versioning.md) |
| 4 | Validation and transport-agnostic authorization | [Validation and authorization](validation-and-authorization.md) |
| 5 | Generated HTTP binding and multipart inputs | [HTTP endpoints](http-endpoints.md), [Attachments](attachments.md) |
| 6 | Code-first gRPC and `.proto` export | [gRPC](grpc.md) |
| 7 | Queue a separate background operation with Rebus | [Rebus](rebus.md) |
| 8 | Incremental results and cancellation | [Streaming](streaming.md), [Server-Sent Events](sse.md) |
| 9 | JSON, MessagePack, protobuf, and generated metadata | [Serialization](serialization.md) |
| 10 | Versioned OpenAPI and Scalar | [OpenAPI](openapi.md) |
| 11 | Isolated Azure Functions HTTP host | [Azure Functions](azure-functions.md) |
| 12 | Reqnroll application and host-boundary testing | [Testing](testing.md), [DOC-01 testing guidance](../progress/tasks/testing/DOC-01-testing-guidance.md) |
| 13 | API-surface review and custom transport adapters | [API-surface snapshots](api-surface-snapshots.md), [Escape hatches](escape-hatches.md) |
| 14 | Generator diagnostics, fallback behavior, and troubleshooting | [Diagnostics and troubleshooting](diagnostics-and-troubleshooting.md) |
| 15 | MCP tools, host composition, and embedded attachments | [MCP](mcp.md) |

## Capability map

| Capability | Contract metadata | Generated/runtime behavior |
| --- | --- | --- |
| HTTP | `[HttpEndpoint]`, `[HttpBody]`, `[HttpQuery]`, `[HttpRoute]` | Minimal API route, binding, status code, multipart support |
| Server-Sent Events | `[Sse]` | Sibling `text/event-stream` route polling the query or framing its stream |
| gRPC | `[GrpcMethod]`, `[GrpcService]`, `[ProtoContract]` | Code-first service, protobuf schema, reflection |
| Native messaging | `[Message]`, `[Event]`, `[MessagingParticipant]`, `[MessagingNetwork]` | Participant-owned routing, generated dispatch, transport-neutral send/publish |
| Rebus | `[ArkRebusHost(typeof(MyParticipant))]` | Generated Rebus routing, scoped adapters, retries, and subscriptions |
| Validation | `IValidator<T>` | Validation decorator before the handler |
| Authorization | `PolicyAuthorizeAttribute` | Shared policy evaluation for HTTP, gRPC, and messages |
| Versioning | `[Versioning]`, `v{version}` route | Version-specific HTTP/OpenAPI/gRPC surfaces |
| Streaming | `IAsyncEnumerable<T>` response | Incremental HTTP JSON and gRPC server stream |
| OpenAPI | XML docs and host options | Versioned documents, schemas, OAuth metadata |
| Serialization | source-generated `JsonSerializerContext` | Explicit JSON metadata and stable transport shapes |
| MCP | `[McpTool]`, assembly marker | Source-generated tools over the official ASP.NET Core MCP SDK |

## What the framework does not hide

You still own:

- dependency-injection registration and lifetimes;
- persistence transactions and outbox boundaries;
- authentication configuration;
- queue topology, retries, and deployment settings;
- application-level tests and cleanup;
- public contract compatibility.

The generator removes repetitive transport plumbing. It does not decide business
ownership or operational policy for you.
