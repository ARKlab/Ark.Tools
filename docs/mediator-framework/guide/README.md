# Mediator Framework guide

This is the task-oriented entry point for teams adopting the source-generated,
MVC-free mediator framework. It keeps one transport-agnostic contract and
handler usable from Minimal API, code-first gRPC, and Rebus. Each page explains
the workflow, shows application-level examples taken from the sample solution,
and states the observable request, response, or generated output to expect.

## Read this guide in order

1. Start with [Getting started](getting-started.md) for the end-to-end workflow.
2. Read [Host setup and composition](host-setup-and-composition.md) before wiring
   a real application host.
3. Read [Request and DTO best practices](request-and-dto-best-practices.md),
   [Application architecture](application-architecture-best-practices.md), and
   [Contracts and handlers](contracts-and-handlers.md) before exposing a
   contract on any transport.
4. Then go to the transport-specific guide for each public interface you expose.

## Choose a starting point

| Need | Read | Expected outcome |
| --- | --- | --- |
| New service | [Getting started](getting-started.md) | One contract served over one or more transports with a verified public call |
| Host wiring, DI, middleware, startup order | [Host setup and composition](host-setup-and-composition.md) | A reproducible `ConfigureServices`/`Configure` setup that matches the sample |
| Contract and handler design | [Contracts and handlers](contracts-and-handlers.md) | Pure handlers with stable contracts and clear trust boundaries |
| Request and DTO composition | [Request and DTO best practices](request-and-dto-best-practices.md) | Versioned models and composed operation envelopes |
| Application layering | [Application architecture](application-architecture-best-practices.md) | Explicit handler transactions, contexts, domain services, and adapters |
| HTTP exposure | [HTTP endpoints](http-endpoints.md) | Generated Minimal API routes, documented status codes, and predictable binding |
| gRPC exposure | [gRPC](grpc.md) | Generated code-first services, exported `.proto`, and reusable test/client setup |
| Rebus exposure | [Rebus](rebus.md) | Generated handlers and routing for asynchronous processing |
| Existing MVC application | [Migration from MVC](../migration-from-mvc.md) | A staged migration plan without rewriting every controller at once |

## Capability map

| Capability | What you write | What the framework generates or enforces | Read | Sample source to inspect |
| --- | --- | --- | --- | --- |
| Versioning | `[Versioning(Introduced, Retired)]` | Versioned HTTP routes, OpenAPI documents, gRPC services, API-surface lifetime entries | [Versioning](versioning.md) | `src/Ark.MediatorFramework.Sample.Application/GreetingContracts.cs` |
| HTTP binding | `[HttpEndpoint]`, `[HttpQuery]`, `[ServerSet]` | Minimal API route/query/body binding, status codes, multipart handling | [HTTP endpoints](http-endpoints.md) | `src/Ark.MediatorFramework.Sample.Application/GreetingContracts.cs`, `AttachmentContracts.cs` |
| gRPC | `[GrpcMethod]`, `[GrpcService]`, `[ProtoContract]` | Code-first services, `.proto` export, rich error mapping | [gRPC](grpc.md) | `src/Ark.MediatorFramework.Sample.WebInterface/SampleStartup.cs`, `test/Ark.MediatorFramework.Sample.GrpcClient` |
| Rebus | `[RebusMessage]` | Rebus handlers, type-based routing helpers, scoped message execution | [Rebus](rebus.md) | `src/Ark.MediatorFramework.Sample.WebInterface/SampleComposition.cs` |
| Validation and authorization | Validators, `PolicyAuthorizeAttribute`, host auth | Decorator-enforced validation and transport-agnostic authorization | [Validation and authorization](validation-and-authorization.md) | `src/Ark.MediatorFramework.Sample.Application/GreetingAuthorizationPolicy.cs` |
| Errors | Domain exceptions and violations | RFC 7807 for HTTP, `google.rpc.Status` for gRPC | [Errors](errors.md) | `test/Ark.MediatorFramework.Sample.Tests/AuthorizationTests.cs` |
| Serialization | JSON defaults, MessagePack opt-in, protobuf metadata | Matching wire shapes per transport | [Serialization](serialization.md) | `PolymorphicContracts.cs`, `GreetingContracts.cs` |
| Attachments | `IArkAttachment` members and `[HttpEndpoint]` limits | Multipart binding, generated downloads, gRPC transfer support | [Attachments](attachments.md) | `AttachmentContracts.cs`, `FileDownloadTests.cs` |
| Streaming | `IAsyncEnumerable<T>` query result | Incremental HTTP JSON and gRPC server streams | [Streaming](streaming.md) | `GreetingContracts.cs`, `AsyncEnumerableStreamingTests.cs` |
| OpenAPI | XML docs plus OpenAPI setup | Versioned documents, OAuth, NodaTime, polymorphism, server-set schema cleanup | [OpenAPI](openapi.md) | `SampleStartup.cs` |
| API-surface review | `ArkApiSurface.txt` baseline | Build-time contract drift detection | [API-surface snapshots](api-surface-snapshots.md) | `src/Ark.MediatorFramework.Sample.Application/ArkApiSurface.txt` |
| Boundary testing | Test host + generated clients | Verified public behavior, auth, errors, and serialization | [Testing](testing.md) | `test/Ark.MediatorFramework.Sample.Tests/Hooks/SampleTestContext.cs` |
| Azure Functions | `[HttpHost]` and isolated worker | Generated Functions triggers, bearer auth, outbound Rebus, and Core Tools boundary tests | [Azure Functions](azure-functions.md) | `src/Ark.MediatorFramework.Sample.AzureFunctions/Program.cs` |
| Escape hatches | Hand-written adapter where needed | Reuse of the same pure handler with custom transport glue | [Escape hatches](escape-hatches.md) | `migration-from-mvc.md` |

## Sample source map

These files are the fastest way to cross-check the guide against a working
application:

- `samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/`
  — contracts, handlers, validators, authorization policies, and application composition.
- `samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/SampleStartup.cs`
  — ASP.NET Core services, middleware, OpenAPI, generated endpoint mapping.
- `samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/SampleComposition.cs`
  — Rebus setup, generated handler registration, transport user-context flow.
- `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/`
  — HTTP, gRPC, streaming, paging, attachment, auth, and concurrency tests.
- `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.GrpcClient/`
  — generated-client project consuming the exported `.proto` files.

Use this framework when one application operation should be exposed on
multiple wires and generated consistently. Use `Ark.Tools.AspNetCore` MVC when
you need MVC controllers, filters, model binders, or an intentionally
controller-shaped application; both can coexist during migration.

Rationale and architecture: [`../design.md`](../design.md).
