# Ark.MediatorFramework.Sample

A minimal, **verifiable** proof of the source-generated, MVC-free web-services
architecture described in [`docs/mvc-free-framework`](../../docs/mvc-free-framework/README.md).

It demonstrates the core thesis: a single **pure, transport-agnostic**
`Ark.Tools.Solid` handler is dispatched identically over two transports —
ASP.NET Core **Minimal API** and **Rebus** — with the hosting code produced by a
**Roslyn incremental source generator**, all wired through a **SimpleInjector**
(non-conforming) container.

## Layout

| Project | Purpose |
|---|---|
| `src/common/Ark.Tools.MediatorFramework` | Runtime package containing the explicit, opt-in `[HttpEndpoint]` / `[RebusMessage]` / `[GrpcMethod]` transport markers and the `IArkAttachment` attachment abstraction. |
| `src/common/Ark.Tools.MediatorFramework.Generators` | Analyzer package containing the incremental generator that emits Minimal API endpoints and Rebus handler wrappers from pure contracts (discovered cross-assembly). |
| `src/Ark.MediatorFramework.Sample.Application` | Pure, transport-agnostic contracts/handlers, in-memory store and cross-cutting decorator. Uses `IContextProvider<ClaimsPrincipal>` for the caller identity. |
| `src/Ark.MediatorFramework.Sample.WebInterface` | Hosting: composition root, ASP.NET Core startup and the endpoints exposing the selected requests/queries. Wires the user context (AspNetCore auth + Rebus propagation) and starts the bus. |
| `test/Ark.MediatorFramework.Sample.Tests` | Self-tests proving transport parity and attachment streaming. |

## What the self-tests prove

- **Minimal API** posts/gets a greeting and hits the pure handler.
- **Rebus** sends the same request message and hits the *same* pure handler and store.
- **Purity**: the handler constructors reference no `Microsoft.AspNetCore`,
  `Rebus`, or `Grpc` types.
- **Source generation**: the endpoint/Rebus registration is `[GeneratedCode]`.
- **Attachments**: a multipart upload is mapped (`IFormFile` → `IArkAttachment`)
  and streamed into the pure handler.
- **OpenAPI**: per-version documents (`/openapi/v1.json`, `/openapi/v2.json`) are
  generated from the endpoint metadata, including NodaTime and polymorphic
  schemas.
- **Polymorphism**: a `[JsonConverter]`-annotated polymorphic contract (via the
  shared `Ark.Tools.SystemTextJson.JsonPolymorphicConverter`) round-trips through
  a generated endpoint.
- **MessagePack**: a hand-written MVC compatibility endpoint negotiates
  `application/x-msgpack` for an existing pure handler.
- **Versioning**: the generator infers the API version from the route template
  (`/api/v{n}/…`) and groups each endpoint into the matching OpenAPI document.

## Run

```bash
dotnet test samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests
```

## Documented follow-ups

The emitted `.proto` now generates a dedicated client assembly used by the
behavioral tests, and the gRPC rich-error interceptor is covered there. Further
work remains specified — with acceptance criteria — in
[`docs/mvc-free-framework/tasks.md`](../../docs/mvc-free-framework/tasks.md).
The NodaTime protobuf surrogates those transports need are already provided by
[`Ark.Tools.Nodatime.Protobuf`](../../src/common/Ark.Tools.Nodatime.Protobuf).
The remaining planned work is tracked as the productization tasks in the
implementation plan.
