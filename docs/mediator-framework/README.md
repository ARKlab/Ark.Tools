# Source-generated, MVC-free web services framework

This folder contains the research, design and delivery plan for an MVC-free,
source-generated web services framework for Ark.Tools. The goal is to host a
single **pure, transport-agnostic handler** over three transports at once —
ASP.NET Core Minimal APIs, code-first gRPC (`protobuf-net.Grpc`) and Rebus
asynchronous message handlers — while keeping business logic completely
isolated from HTTP translation, serialization and routing.

## Documents

Reference documentation (**what the framework is**) lives in this folder.
Everything that tracks **how it is being built** — plans, task boards, progress,
reviews — lives in [`progress/`](progress/README.md).

### Reference

| Document | Purpose |
| --- | --- |
| [`design.md`](design.md) | Target architecture: pure handlers, Roslyn incremental generator, the three transports, DI, error handling, user context, attachments. |
| [`research.md`](research.md) | Evaluation of open-source alternatives, comparison with gRPC JSON transcoding, capability/library mapping. |
| [`migration-from-mvc.md`](migration-from-mvc.md) | Incremental migration guidance, including the MVC compatibility escape hatch. |

The end-user guide (getting started + per-feature documentation) is delivered by
task [`DOC-01`](progress/tasks/docs/DOC-01-user-documentation.md) and will live
in `docs/mediator-framework/guide/`.

### Progress and tracking

| Document | Purpose |
| --- | --- |
| [`progress/README.md`](progress/README.md) | Index of all delivery tracking documents. |
| [`progress/implementation-plan.md`](progress/implementation-plan.md) | Phased delivery plan with the packages to introduce. |
| [`progress/tasks.md`](progress/tasks.md) | Verifiable task breakdown with explicit acceptance criteria (epics). |
| [`progress/tasks/README.md`](progress/tasks/README.md) | Task board: one self-contained task document per item, organized by category, with Outcomes and Acceptance criteria. |
| [`progress/pre-release-review.md`](progress/pre-release-review.md) | Adversarial pre-release review (DX + security), feature-gap analysis vs Ark.ReferenceProject, and recorded decisions. |
| [`progress/future-improvements.md`](progress/future-improvements.md) | Explicitly deferred post-1.0 items. |

## Verifiable sample

A runnable proof-of-concept lives in
[`samples/Ark.MediatorFramework.Sample`](../../samples/Ark.MediatorFramework.Sample).
It demonstrates the same pure handler being invoked over Minimal API, generated
code-first gRPC and Rebus, plus a hand-written MessagePack compatibility
endpoint, wired through SimpleInjector, and **self-tests every implemented
transport** so the outcome is verifiable with `dotnet test`.

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
