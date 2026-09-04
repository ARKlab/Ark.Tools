# Ark.Tools SDK

This folder contains the accepted design and delivery plan for distributing the
Ark.Tools solution build conventions as versioned packages.

## Documents

| Document | Purpose |
| --- | --- |
| [`design.md`](design.md) | Current Ark.Tools defaults, packaging alternatives, upstream research, and accepted architecture. |
| [`privacy-by-default-prd.md`](privacy-by-default-prd.md) | Approved PII/secret protection product (`Ark.Tools.Compliance`): research, developer experience, `ARKPII*` analyzers, runtime redaction, Vogen analysis, rejected approaches. |
| [`progress/README.md`](progress/README.md) | Delivery tracking rules and document index. |
| [`progress/decisions.md`](progress/decisions.md) | Accepted product and compatibility decisions. |
| [`progress/tasks/README.md`](progress/tasks/README.md) | Canonical implementation task board. |
| [`mtp.md`](mtp.md) | MTP test-profile defaults, extension switches, and CI responsibilities. |

Implementation is split into independently reviewable tasks; each task owns its
execution map, tests, outcomes, and acceptance criteria.
