# Ark.Tools SDK — progress

This directory tracks delivery. Stable architecture and accepted defaults
remain in [`../design.md`](../design.md).

| Document | Purpose |
| --- | --- |
| [`decisions.md`](decisions.md) | Accepted decisions and rejected alternatives. |
| [`tasks/README.md`](tasks/README.md) | **Canonical current task board.** One status and one link per implementation task. |
| [`../privacy-by-default-prd.md`](../privacy-by-default-prd.md) | Approved compliance/privacy design behind the `PII-IMP` tasks. |

## Status rules

- Each task file owns its Execution map, Outcomes, and Acceptance content.
- [`tasks/README.md`](tasks/README.md) is the only current status board.
- `Complete` means every acceptance checkbox is checked; `In progress` means
  acceptance has checked and unchecked items; `Pending` means none are checked.
- Design and decision documents record rationale and constraints; they are not
  duplicated as progress summaries.

## Working agreement

- Execute each task in its own branch/PR with a Conventional Commit title.
- Keep the repository runnable after every task.
- Do not activate `Ark.Tools.Sdk` across existing repository projects before the
  migration task.
- Every implementation task runs:
  `dotnet build Ark.Tools.slnx --configuration Debug` and
  `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`.
- A task that changes accepted behavior updates [`../design.md`](../design.md)
  and [`decisions.md`](decisions.md) in the same PR.
