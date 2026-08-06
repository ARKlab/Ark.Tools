# TST-01 — Approve ownership and update the delivery map

**Depends on:** Decision log approval (complete)
**Scope:** Documentation and progress tracking

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

1. Record the accepted D1–D7 decisions from
   `docs/mediator-framework/progress/mediator-testing-decisions.md`.
2. Add a testing-redesign workstream to
   `docs/mediator-framework/progress/tasks/README.md` with links to one task
   document per implementation task.
3. Mark the old T9.8 boundary-test wording in
   `docs/mediator-framework/progress/tasks.md` as superseded by this workstream;
   preserve the historical acceptance text and link to the new plan.
4. Keep the full-solution build/test gate and locked-restore rules visible in
   the new task entries.

## Outcome

- Reviewers can see who owns each test category and which accepted decisions
  govern implementation before code moves.

## Acceptance

- [x] Every D1–D7 has an explicit accepted option or an owner and due decision.
- [x] The task board has a unique ID and dependency order for every task below.
- [x] No task claims that sample application tests must assert a URL, status,
  ProblemDetails wire body, serialization, or OpenAPI document.
- [x] Existing progress links remain valid.

## Tests

- Check every new relative Markdown link with a repository-wide path search.
- Run `git diff --check`.
- Required scenarios/cases:
  - each D1–D7 has one accepted option recorded in the decision log;
  - the delivery map links every planned task and the superseded T9.8 wording;
  - no application-test task assigns ownership of transport URLs, statuses,
    ProblemDetails wire bodies, serialization, or OpenAPI.
- Run the full documentation-independent build/test gate.
