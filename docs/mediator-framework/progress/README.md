# Mediator Framework — delivery tracking

Everything in this folder tracks **how** the framework is being built. The
reference documentation (what the framework *is*) stays one level up:
[`../design.md`](../design.md), [`../research.md`](../research.md),
[`../migration-from-mvc.md`](../migration-from-mvc.md).

| Document | Purpose |
| --- | --- |
| [`tasks/README.md`](tasks/README.md) | **Current task board.** One self-contained task document per pending item, with Outcomes and Acceptance. Start here. |
| [`tasks.md`](tasks.md) | Historical epic breakdown (Epics 1–12) with acceptance criteria. |
| [`implementation-plan.md`](implementation-plan.md) | Phased delivery plan (Phases 1–10) with step-by-step instructions. |
| [`pre-release-review.md`](pre-release-review.md) | Adversarial pre-release review (DX + security), gap analysis vs `Ark.ReferenceProject`, recorded decisions D1–D8. |
| [`future-improvements.md`](future-improvements.md) | Explicitly deferred post-1.0 items. |
| [`azure-functions-decision-log.md`](azure-functions-decision-log.md) | Reviewable decisions for the proposed Azure Functions hosting workstream. |

## Working agreement

- Every task is executed in its own branch/PR with a conventional-commit title.
- Full-solution build gate on every step:
  `dotnet build Ark.Tools.slnx --configuration Debug` (zero warnings) and
  `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`.
- A task that changes framework behavior described in [`../design.md`](../design.md)
  updates that document in the same PR.
- Progress is recorded in [`tasks/README.md`](tasks/README.md) (checkbox in the
  execution order) — that file is the single source of truth for "what is left".
