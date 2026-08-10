# Mediator Framework — delivery plan and workstream map

This document records the delivery sequence and ownership boundaries. The
executable instructions and acceptance criteria live only in the linked task
files; current status lives only in [`tasks/README.md`](tasks/README.md).

## Delivery sequence

| Phase | Feature sequence | Result |
| --- | --- | --- |
| 1 | Pure handlers, SimpleInjector composition, Minimal API, and Rebus | Initial transport-agnostic proof of concept |
| 2 | Roslyn generators and generated endpoint registration | Compile-time Minimal API and Rebus integration |
| 3 | Code-first gRPC, exported protos, generated clients, and rich errors | Third transport and published wire contract |
| 4 | User context, ProblemDetails, dead letters, OpenAPI, attachments, and NodaTime | Cross-cutting and schema coverage |
| 5 | Package extraction, lock files, validation, SBOM, and MVC migration guidance | Productized framework packages |
| 6 | Review revisions and framework capability tests | Review-driven parity and framework-owned hosting tests |
| 7 | Preview follow-ups | Rebus routing, source-generated JSON metadata, authenticated OpenAPI UIs, and gRPCui operations |
| 8 | Release-scope extension | OpenAPI taxonomy, standard responses, streaming, multi-file uploads, XML docs, API snapshots, and user guide |
| 9 | Azure Functions isolated-worker hosting | New host workstream governed by the Azure decisions and task board |
| 10 | Testing redesign | Direct application-contract testing and framework-owned hosting coverage |

The historical details for phases 1–8 are preserved in
[`tasks.md`](tasks.md). They are intentionally not duplicated here.

## Workstream ownership

| Workstream | Decision or analysis | Executable tasks |
| --- | --- | --- |
| Review and release scope | [`pre-release-review.md`](pre-release-review.md) | [`tasks/README.md`](tasks/README.md) |
| Minimal API hosting defaults | [`aspnetcore-hosting-gap-analysis.md`](aspnetcore-hosting-gap-analysis.md) | [`tasks/README.md`](tasks/README.md), `tasks/aspnetcore/HST-*.md` |
| Azure Functions hosting | [`azure-functions-decision-log.md`](azure-functions-decision-log.md), [`../azure-functions-design.md`](../azure-functions-design.md) | `tasks/azure-functions/AZF-*.md` |
| Testing redesign | [`mediator-testing-decisions.md`](mediator-testing-decisions.md), [`mediator-testing-plan.md`](mediator-testing-plan.md) | `tasks/testing/TST-*.md`, `tasks/testing/APP-*.md` |
| Deferred improvements | [`future-improvements.md`](future-improvements.md) | Future and post-release sections of [`tasks/README.md`](tasks/README.md) |

## Delivery rules

- A task is complete only when its own acceptance checklist is complete.
- Every task uses the repository build and test gates:
  `dotnet build Ark.Tools.slnx --configuration Debug` and
  `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`.
- Dependency changes also update central versions and lock files.
- Behavior changes update [`../design.md`](../design.md) in the same change.
- Design and decision documents own rationale; this file does not restate it.

## Validation order

1. Resolve or confirm the applicable decision document.
2. Follow the dependency order in the task board.
3. Implement and validate one self-contained task.
4. Update the task board status from the task file's acceptance state.
5. Add user documentation after the shipped behavior is stable.
