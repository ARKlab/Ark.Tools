# Source-generated, MVC-free web services framework

This folder contains the research and design record for an MVC-free,
source-generated web services framework for Ark.Tools. The goal is to host a
single **pure, transport-agnostic handler** over three transports at once —
ASP.NET Core Minimal APIs, code-first gRPC (`protobuf-net.Grpc`) and Rebus
asynchronous message handlers — while keeping business logic completely
isolated from HTTP translation, serialization and routing.

## Documents

Reference documentation (**what the framework is**) lives in
[`docs/mediator-framework/`](../../mediator-framework/README.md). Everything
that tracks **how it is being built** — plans, task boards, progress, reviews —
lives in [`docs/plans/mediator-framework/`](../../plans/mediator-framework/README.md).

### Reference

| Document | Purpose |
| --- | --- |
| [`design.md`](design.md) | Target architecture: pure handlers, Roslyn incremental generators, transports, DI, error handling, user context, attachments. |
| [`azure-functions-design.md`](azure-functions-design.md) | Proposed .NET isolated Azure Functions HTTP hosting architecture and parity contract. |
| [`mcp-design.md`](mcp-design.md) | Proposed source-generated MCP tool bridge using the official ASP.NET Core MCP SDK. |
| [`messaging-throughput-prd.md`](messaging-throughput-prd.md) | Proposed high-throughput messaging receivers: receive/processing seam split, adaptive concurrency, credit-bounded prefetch, lock renewal, transport profiles. |
| [`research.md`](research.md) | Evaluation of open-source alternatives, comparison with gRPC JSON transcoding, capability/library mapping. |
| [`migration-from-mvc.md`](../../mediator-framework/migration-from-mvc.md) | Incremental migration guidance, including the MVC compatibility escape hatch. |

Start with the [Mediator Framework user guide](../../mediator-framework/README.md). The
[how-it-works](../../mediator-framework/how-it-works/README.md) section documents the mechanics behind
the features for anyone debugging, extending, or changing them.

### Plans and tracking

| Document | Purpose |
| --- | --- |
| [`plans/README.md`](../../plans/mediator-framework/README.md) | Index of all delivery tracking documents. |
| [`plans/implementation-plan.md`](../../plans/mediator-framework/implementation-plan.md) | Delivery sequence and workstream ownership map. |
| [`plans/tasks.md`](../../plans/mediator-framework/tasks.md) | Historical epic index and feature sequence. |
| [`plans/tasks/README.md`](../../plans/mediator-framework/tasks/README.md) | Canonical current task board with one status and link per task. |
| [`plans/pre-release-review.md`](../../plans/mediator-framework/pre-release-review.md) | Adversarial pre-release review (DX + security), feature-gap analysis vs Ark.ReferenceProject, and recorded decisions. |
| [`plans/aspnetcore-hosting-gap-analysis.md`](../../plans/mediator-framework/aspnetcore-hosting-gap-analysis.md) | Accepted Minimal API hosting gap analysis and startup decisions. |
| [`plans/azure-functions-decision-log.md`](../../plans/mediator-framework/azure-functions-decision-log.md) | Accepted Azure Functions hosting decisions. |
| [`plans/mediator-testing-plan.md`](../../plans/mediator-framework/mediator-testing-plan.md) | Testing architecture and implementation boundaries. |
| [`plans/mediator-testing-decisions.md`](../../plans/mediator-framework/mediator-testing-decisions.md) | Accepted testing architecture decisions. |
| [`plans/future-improvements.md`](../../plans/mediator-framework/future-improvements.md) | Explicitly deferred post-1.0 items. |

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
