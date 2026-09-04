# Ark.Tools SDK

The Ark.Tools SDK is the versioned, additional MSBuild SDK that standardizes
build quality, analyzer configuration, packaging, content handling, and
framework-neutral MTP test execution for Ark-owned .NET solutions.

Start with the [capability reference](reference.md), which documents activation,
defaults, conditions, evaluation timing, direct overrides, package references,
and adoption steps.

## Documents

| Document | Purpose |
| --- | --- |
| [`design.md`](design.md) | Current Ark.Tools defaults, packaging alternatives, upstream research, and accepted architecture. |
| [`reference.md`](reference.md) | Stable consumer capability and property reference. |
| [`privacy-by-default-prd.md`](privacy-by-default-prd.md) | Approved PII/secret protection product (`Ark.Tools.Compliance`): research, developer experience, `ARKPII*` analyzers, runtime redaction, Vogen analysis, rejected approaches. |
| [`progress/README.md`](progress/README.md) | Delivery tracking rules and document index. |
| [`progress/decisions.md`](progress/decisions.md) | Accepted product and compatibility decisions. |
| [`progress/tasks/README.md`](progress/tasks/README.md) | Canonical implementation task board. |
| [`mtp.md`](mtp.md) | MTP test-profile defaults, extension switches, and CI responsibilities. |

## Packages

- `Ark.Tools.Sdk` is the additional SDK. Pin it in `global.json` and add
  `<Sdk Name="Ark.Tools.Sdk" />` beside the primary project SDK.
- `Ark.Tools.Build` is the dependency-free transitive build-policy package
  injected by `Ark.Tools.Sdk`.
- The package pages use the concise [repository README](../../README.md);
  the complete property table is the stable [capability reference](reference.md).

Implementation is split into independently reviewable tasks; each task owns its
execution map, tests, outcomes, and acceptance criteria.
