# Ark.Tools SDK — current task board

Each task document is self-contained. Its Execution map, Outcomes, and
Acceptance section are authoritative; this board records only sequence,
category, current status, and a link.

The accepted architecture is [`../../design.md`](../../design.md). Accepted
choices and rejected alternatives are in
[`../decisions.md`](../decisions.md).

## Execution rules

- Implement one task per branch/PR using the listed order and dependencies.
- Extend the shared `tests/Ark.Tools.Sdk.Tests` clean-consumer fixture instead
  of creating task-specific test harnesses or scripts.
- Pack test artifacts into an isolated local feed and use an isolated global
  packages folder. Tests must not depend on packages left by an earlier run.
- Keep `Ark.Tools.Build` dependency-free and public; keep restore-affecting
  behavior in `Ark.Tools.Sdk`.
- Do not activate the new SDK in existing repository projects until
  SDK-IMP-09.
- Update the task file and this board together. Status is derived only from the
  task's acceptance checkboxes.
- Every task leaves these gates green:
  `dotnet build Ark.Tools.slnx --configuration Debug` and
  `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`.

## Status legend

| Status | Meaning |
| --- | --- |
| Complete | Every acceptance checkbox in the task file is checked. |
| In progress | The task file has checked and unchecked acceptance items. |
| Pending | The task file has no checked acceptance items. |
| Cancelled | The task was explicitly cancelled in its task file. |
| Deferred | The task is intentionally deferred by an accepted decision. |

## Delivery sequence

| Task | Title | Category | Status |
| --- | --- | --- | --- |
| [SDK-IMP-01](SDK-IMP-01-dual-package-foundation.md) | Dual-package and clean-consumer test foundation | Foundation | Complete |
| [SDK-IMP-02](SDK-IMP-02-public-build-baseline.md) | Public `Ark.Tools.Build` safety baseline | Build policy | Complete |
| [SDK-IMP-03](SDK-IMP-03-analyzer-configuration-assets.md) | Analyzer configuration, banned APIs, and safety targets | Build policy | Complete |
| [SDK-IMP-04](SDK-IMP-04-sdk-restore-and-analyzers.md) | SDK restore policy and analyzer ownership | SDK policy | Complete |
| [SDK-IMP-05](SDK-IMP-05-source-and-packaging-profile.md) | Source, build, and packaging tool profile | SDK policy | Complete |
| [SDK-IMP-06](SDK-IMP-06-mtp-test-profile.md) | Framework-neutral MTP test profile | Testing | Complete |
| [SDK-IMP-07](SDK-IMP-07-content-and-reqnroll-profile.md) | Application settings and Reqnroll profile | Content | Complete |
| [SDK-IMP-08](SDK-IMP-08-compatibility-and-release-gate.md) | Compatibility matrix and paired-package release gate | Validation | Pending |
| [SDK-IMP-09](SDK-IMP-09-reference-project-migration.md) | ReferenceProject migration | Migration | Pending |
| [SDK-IMP-10](SDK-IMP-10-documentation-and-adoption.md) | Consumer documentation and adoption guidance | Documentation | Pending |

SDK-IMP-05, SDK-IMP-06, and SDK-IMP-07 can proceed in parallel after
SDK-IMP-04.
SDK-IMP-09 starts only after the same preview version of `Ark.Tools.Sdk` and
`Ark.Tools.Build` is available from an Ark package source; MSBuild must resolve
the SDK before project targets can build a local replacement.
