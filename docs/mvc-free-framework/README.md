# Source-generated, MVC-free web services framework

This folder contains the research, design and delivery plan for an MVC-free,
source-generated web services framework for Ark.Tools. The goal is to host a
single **pure, transport-agnostic handler** over three transports at once —
ASP.NET Core Minimal APIs, code-first gRPC (`protobuf-net.Grpc`) and Rebus
asynchronous message handlers — while keeping business logic completely
isolated from HTTP translation, serialization and routing.

## Documents

| Document | Purpose |
| --- | --- |
| [`research.md`](research.md) | Evaluation of open-source alternatives, comparison with gRPC JSON transcoding, capability/library mapping. |
| [`design.md`](design.md) | Target architecture: pure handlers, Roslyn incremental generator, the three transports, DI, error handling, user context, attachments. |
| [`implementation-plan.md`](implementation-plan.md) | Phased delivery plan with the packages to introduce. |
| [`tasks.md`](tasks.md) | Verifiable task breakdown with explicit acceptance criteria. |

## Verifiable sample

A runnable proof-of-concept lives in
[`samples/Ark.MediatorFramework.Sample`](../../samples/Ark.MediatorFramework.Sample).
It demonstrates the same pure handler being invoked over Minimal API, gRPC and
Rebus, wired through SimpleInjector, and **self-tests every transport** so the
outcome is verifiable with `dotnet test`.

## Relationship with existing Ark.Tools building blocks

The design deliberately reuses what Ark.Tools already ships instead of inventing
new abstractions:

- **`Ark.Tools.Solid`** already defines `IRequest<T>`/`IRequestHandler<,>`,
  `IQuery<T>`/`IQueryHandler<,>` and `ICommand`/`ICommandHandler<>`. These are
  the "pure handler" contracts. The current `IRequestProcessor`/`IQueryProcessor`
  implementations dispatch **dynamically** (they are annotated
  `[RequiresUnreferencedCode]`) — that runtime reflection is exactly the tax the
  source generator removes.
- **`Ark.Tools.SimpleInjector`** / **`Ark.Tools.Solid.SimpleInjector`** provide
  the non-conforming container and decorator registration used for cross-cutting
  concerns.
- **`Ark.Tools.Rebus`** / **`Ark.Tools.Outbox.Rebus`** provide the messaging
  infrastructure the generated Rebus wrappers plug into, including the
  per-message SimpleInjector scope (`RebusScopeDecorator<>`).
