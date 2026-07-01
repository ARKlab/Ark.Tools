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
| `src/Ark.MediatorFramework` | Runtime primitives: `[ArkEndpoint]` marker and `IUserContext`. |
| `src/Ark.MediatorFramework.Generators` | Incremental generator emitting the Minimal API endpoints and Rebus handler wrappers from the pure contracts. |
| `src/Ark.MediatorFramework.Sample.Api` | Pure contracts/handlers, in-memory store, cross-cutting decorator and composition root. |
| `test/Ark.MediatorFramework.Sample.Tests` | Self-tests proving transport parity. |

## What the self-tests prove

- **Minimal API** posts/gets a greeting and hits the pure handler.
- **Rebus** sends the same request message and hits the *same* pure handler and store.
- **Purity**: the handler constructors reference no `Microsoft.AspNetCore`,
  `Rebus`, or `Grpc` types.
- **Source generation**: the endpoint/Rebus registration is `[GeneratedCode]`.

## Run

```bash
dotnet test samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests
```

## Documented follow-ups

gRPC (code-first `protobuf-net.Grpc`) transport, `.proto` emission, gRPC
rich-error interceptor, per-transport `IUserContext` seeding, `ProblemDetails`
mapping, Rebus dead-letter behavior, OpenAPI generation and attachment
(`IFormFile` → `IProxyStream`) mapping are specified — with acceptance criteria —
in [`docs/mvc-free-framework/tasks.md`](../../docs/mvc-free-framework/tasks.md).
They require new third-party dependencies and are intentionally left as the next
increments.
