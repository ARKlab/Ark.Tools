# Mediator Framework — delivery tracking

This directory tracks delivery. The framework reference documentation remains
one level up:
[`../design.md`](../design.md), [`../research.md`](../research.md), and
[`../migration-from-mvc.md`](../migration-from-mvc.md).

## Delivery sequence

| Sequence | Workstream | Tracking |
| --- | --- | --- |
| 1 | Initial implementation and productization: pure handlers, Minimal API, Rebus, gRPC, cross-cutting behavior, OpenAPI, attachments, and packages | [`tasks.md`](tasks.md) |
| 2 | Review-driven gap analysis and release scope | [`pre-release-review.md`](pre-release-review.md) |
| 3 | Minimal API hosting parity and startup defaults | [`aspnetcore-hosting-gap-analysis.md`](aspnetcore-hosting-gap-analysis.md) and [`tasks/README.md`](tasks/README.md) |
| 4 | Azure Functions isolated-worker hosting | [`azure-functions-decision-log.md`](azure-functions-decision-log.md), [`azure-functions-messaging-design.md`](azure-functions-messaging-design.md), and [`tasks/README.md`](tasks/README.md) |
| 5 | Framework hosting tests and direct application tests | [`mediator-testing-decisions.md`](mediator-testing-decisions.md), [`mediator-testing-plan.md`](mediator-testing-plan.md), and [`tasks/README.md`](tasks/README.md) |
| 6 | Deferred and post-release work | [`future-improvements.md`](future-improvements.md) and [`tasks/README.md`](tasks/README.md) |

## Tracking documents

| Document | Purpose |
| --- | --- |
| [`tasks/README.md`](tasks/README.md) | **Canonical current task board.** One status and one link per task. |
| [`tasks.md`](tasks.md) | Historical epic index and feature sequence; not a second status board. |
| [`implementation-plan.md`](implementation-plan.md) | Historical delivery record and workstream map; executable detail stays in task files. |
| [`pre-release-review.md`](pre-release-review.md) | Adversarial review, feature gaps, and decisions D1–D9. |
| [`aspnetcore-hosting-gap-analysis.md`](aspnetcore-hosting-gap-analysis.md) | Accepted Minimal API hosting gap analysis and HSD decisions. |
| [`azure-functions-decision-log.md`](azure-functions-decision-log.md) | Accepted Azure Functions hosting decisions. |
| [`azure-functions-messaging-design.md`](azure-functions-messaging-design.md) | Azure Functions Service Bus/Storage Queue messaging design baseline. |
| [`mediator-testing-decisions.md`](mediator-testing-decisions.md) | Accepted testing architecture decisions. |
| [`mediator-testing-plan.md`](mediator-testing-plan.md) | Testing architecture and implementation boundaries. |
| [`future-improvements.md`](future-improvements.md) | Explicitly deferred post-1.0 work. |

## Status rules

- The individual task file owns its Outcomes and Acceptance content.
- [`tasks/README.md`](tasks/README.md) is the only current status board.
- `Complete` means every acceptance checkbox in the task file is checked;
  `In progress` means the task has both checked and unchecked acceptance items;
  `Pending` means none are checked. Explicitly cancelled or deferred tasks keep
  those labels.
- Design and decision documents record rationale and constraints; they are not
  duplicated or rewritten as progress summaries.

## Working agreement

- Every task is executed in its own branch/PR with a conventional-commit title.
- Full-solution build gate:
  `dotnet build Ark.Tools.slnx --configuration Debug` and
  `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`.
- A task that changes behavior described in [`../design.md`](../design.md)
  updates that document in the same PR.
- Every AZM task updates its assigned guide section and the existing Book sample
  surface described by its Execution map; AZM-16 performs only final
  integration/review.
