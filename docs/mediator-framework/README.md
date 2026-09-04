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
| [`design.md`](design.md) | Target architecture: pure handlers, Roslyn incremental generators, transports, DI, error handling, user context, attachments. |
| [`azure-functions-design.md`](azure-functions-design.md) | Proposed .NET isolated Azure Functions HTTP hosting architecture and parity contract. |
| [`mcp-design.md`](mcp-design.md) | Proposed source-generated MCP tool bridge using the official ASP.NET Core MCP SDK. |
| [`messaging-throughput-prd.md`](messaging-throughput-prd.md) | Proposed high-throughput messaging receivers: receive/processing seam split, adaptive concurrency, credit-bounded prefetch, lock renewal, transport profiles. |
| [`research.md`](research.md) | Evaluation of open-source alternatives, comparison with gRPC JSON transcoding, capability/library mapping. |
| [`migration-from-mvc.md`](migration-from-mvc.md) | Incremental migration guidance, including the MVC compatibility escape hatch. |

Start with the [Mediator Framework user guide](guide/README.md).

### Progress and tracking

| Document | Purpose |
| --- | --- |
| [`progress/README.md`](progress/README.md) | Index of all delivery tracking documents. |
| [`progress/implementation-plan.md`](progress/implementation-plan.md) | Delivery sequence and workstream ownership map. |
| [`progress/tasks.md`](progress/tasks.md) | Historical epic index and feature sequence. |
| [`progress/tasks/README.md`](progress/tasks/README.md) | Canonical current task board with one status and link per task. |
| [`progress/pre-release-review.md`](progress/pre-release-review.md) | Adversarial pre-release review (DX + security), feature-gap analysis vs Ark.ReferenceProject, and recorded decisions. |
| [`progress/aspnetcore-hosting-gap-analysis.md`](progress/aspnetcore-hosting-gap-analysis.md) | Accepted Minimal API hosting gap analysis and startup decisions. |
| [`progress/azure-functions-decision-log.md`](progress/azure-functions-decision-log.md) | Accepted Azure Functions hosting decisions. |
| [`progress/mediator-testing-plan.md`](progress/mediator-testing-plan.md) | Testing architecture and implementation boundaries. |
| [`progress/mediator-testing-decisions.md`](progress/mediator-testing-decisions.md) | Accepted testing architecture decisions. |
| [`progress/future-improvements.md`](progress/future-improvements.md) | Explicitly deferred post-1.0 items. |

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
