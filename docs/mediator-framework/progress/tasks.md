# Mediator Framework — historical epic index

This file preserves the original delivery sequence. It is not a second task
board: current status, acceptance, dependencies, and links are maintained in
[`tasks/README.md`](tasks/README.md) and the individual task files.

## 1. Initial implementation and productization

The first delivery established the framework and sample:

1. Pure request/query handlers and SimpleInjector composition.
2. Generated Minimal API endpoints.
3. Generated Rebus wrappers and dead-letter behavior.
4. Code-first gRPC services, proto export, generated client coverage, and
   NodaTime protobuf support.
5. Cross-transport user context and ProblemDetails behavior.
6. OpenAPI, versioning, polymorphism, attachments, and MessagePack.
7. Runtime/generator extraction into transport packages, package validation,
   lock files, and MVC migration guidance.

The original detailed acceptance lists are intentionally not repeated here.
Use the current task documents for executable work.

## 2. Review-driven revisions

The review sequence added the following feature groups:

- NodaTime and ProblemDetails parity.
- gRPC rich errors and streaming uploads.
- Version lifetime and `{version}` routing.
- Per-transport package and framework-test separation.
- Rebus routing, source-generated JSON metadata, authenticated OpenAPI UIs,
  and the gRPCui operations workflow.

The review findings and decisions are recorded in
[`pre-release-review.md`](pre-release-review.md). Their current executable
follow-ups are in the categorized board.

## 3. Release-scope extension

The later release scope introduced:

- OpenAPI tags and operation names.
- Standard ProblemDetails responses.
- `IAsyncEnumerable<T>` streaming.
- Multi-file uploads.
- XML documentation in OpenAPI and exported protos.
- API-surface snapshots.
- User documentation.

These are tracked individually under the release-scope section of
[`tasks/README.md`](tasks/README.md); no aggregate checkbox is maintained here.

## 4. Additional delivery workstreams

The implementation sequence was extended without changing the original task
documents:

| Workstream | Scope | Tracking |
| --- | --- | --- |
| Minimal API hosting gap analysis | Startup composition, security defaults, proxy handling, health, compression, telemetry, logging, and diagnostics | [`aspnetcore-hosting-gap-analysis.md`](aspnetcore-hosting-gap-analysis.md) |
| Azure Functions addition | Isolated-worker host, generated triggers, shared HTTP semantics, authentication, results, files, Rebus, and boundary parity | [`azure-functions-decision-log.md`](azure-functions-decision-log.md) |
| Testing support | Framework-owned hosting tests and direct application-contract tests | [`mediator-testing-plan.md`](mediator-testing-plan.md) and [`mediator-testing-decisions.md`](mediator-testing-decisions.md) |

## Current source of truth

1. Start with [`tasks/README.md`](tasks/README.md) for the current status.
2. Open the linked task file for the acceptance contract.
3. Use the workstream decision or analysis document for rationale.
4. Use [`../design.md`](../design.md) for framework behavior and wire contracts.

The old epic numbering remains useful for historical context, but it must not
be used to infer that every task in an epic is complete.
