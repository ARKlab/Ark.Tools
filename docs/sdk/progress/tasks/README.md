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
| [SDK-IMP-08](SDK-IMP-08-compatibility-and-release-gate.md) | Compatibility matrix and paired-package release gate | Validation | Cancelled |
| [SDK-IMP-09](SDK-IMP-09-reference-project-migration.md) | ReferenceProject migration | Migration | Complete |
| [SDK-IMP-10](SDK-IMP-10-documentation-and-adoption.md) | Consumer documentation and adoption guidance | Documentation | Pending |
| [SDK-IMP-11](SDK-IMP-11-sonaranalyzer-csharp-evaluation.md) | `SonarAnalyzer.CSharp` evaluation | Analyzer evaluation | Pending analysis (draft) |
| [SDK-IMP-12](SDK-IMP-12-devskim-evaluation.md) | `Microsoft.CST.DevSkim` evaluation | Analyzer evaluation | Pending analysis (draft) |

SDK-IMP-05, SDK-IMP-06, and SDK-IMP-07 can proceed in parallel after
SDK-IMP-04.
SDK-IMP-09 starts after SDK-IMP-07 and uses the repository source-build
arrangement; it does not require a published preview pair.

## Compliance (privacy by default)

Approved design: [`../../privacy-by-default-prd.md`](../../privacy-by-default-prd.md).
These tasks are independent of the `SDK-IMP` sequence except for PII-IMP-10,
which packages configuration through `Ark.Tools.Build` and therefore follows
SDK-IMP-03's asset conventions.

| Task | Title | Category | Status |
| --- | --- | --- | --- |
| [PII-IMP-01](compliance/PII-IMP-01-compliance-foundation.md) | Compliance foundation: attributes, taxonomy, redactors | Foundation | Done |
| [PII-IMP-02](compliance/PII-IMP-02-sensitive-value-object-generator.md) | Sensitive value-object generator | Generator | Pending |
| [PII-IMP-03](compliance/PII-IMP-03-serialization-targets.md) | Serialization targets incl. OpenAPI/Swashbuckle | Generator | Pending |
| [PII-IMP-04](compliance/PII-IMP-04-declaration-tier-analyzers.md) | Declaration-tier analyzers and code fixes | Analyzer | Pending |
| [PII-IMP-05](compliance/PII-IMP-05-sink-tier-analyzers.md) | Sink-tier analyzers (logs, exceptions) | Analyzer | Pending |
| [PII-IMP-06](compliance/PII-IMP-06-compliance-surface-gate.md) | Compliance surface inventory and gate | Tooling | Pending |
| [PII-IMP-07](compliance/PII-IMP-07-runtime-redaction.md) | Runtime redaction: NLog pipeline and OTel processor | Runtime | Pending |
| [PII-IMP-08](compliance/PII-IMP-08-sql-policy-generation.md) | SQL policy attributes and script generation | Persistence | Pending |
| [PII-IMP-09](compliance/PII-IMP-09-test-data-rules.md) | Test data rules and reserved-value fakes | Testing | Pending |
| [PII-IMP-10](compliance/PII-IMP-10-sdk-wiring-and-loggen-guards.md) | SDK wiring, LOGGEN guards, documentation | SDK policy | Pending |
| [PII-IMP-11](compliance/PII-IMP-11-reference-project-adoption.md) | ReferenceProject adoption and end-to-end proof | Migration | Pending |

PII-IMP-01 unblocks everything. PII-IMP-02, PII-IMP-04, and PII-IMP-07 can then
proceed in parallel; PII-IMP-03 follows PII-IMP-02, and PII-IMP-05, PII-IMP-06,
PII-IMP-08, and PII-IMP-09 follow PII-IMP-04. PII-IMP-10 needs the analyzer,
surface, and runtime layers before it can package their defaults, and
PII-IMP-11 is last because it consumes all of them.

Upstream Vogen contributions are **not** on this board: Ark.Tools does not use
Vogen, so they are recorded as a draft in
[`../future-improvements.md`](../future-improvements.md) instead.
