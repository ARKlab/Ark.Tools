# PII-IMP-01 — Compliance foundation: attributes, taxonomy, redactors

**Category**: compliance-foundation · **Priority**: foundation
**Depends on**: none
**Scope**: NEW PACKAGE + TESTS
**Design**: [Declaring what is sensitive](../../../privacy-by-default-prd.md#61-declaring-personal-data),
[Packaging](../../../privacy-by-default-prd.md#9-packaging),
[Decisions PII‑01/PII‑03](../../../privacy-by-default-prd.md#17-decisions)

## Problem

Every later task keys off one classification vocabulary. Without it there is no
symbol for an analyzer to match, no attribute for a generator to read, and no
shared redaction primitive, so the vocabulary must exist first and must be the
Microsoft one rather than a parallel Ark taxonomy.

## Execution map

- **Package**: `Ark.Tools.Compliance`, `net8.0;net10.0`, dependency on
  `Microsoft.Extensions.Compliance.Abstractions` (decision PII‑01). Add the
  package to `Directory.Packages.props` and refresh every affected
  `packages.lock.json` in the same commit; CI restores with
  `RestoreLockedMode=true`.
- **Attributes**: `PersonalDataAttribute`, `SensitivePersonalDataAttribute`,
  `SecretAttribute`, `PseudonymousAttribute`, each deriving from
  `DataClassificationAttribute` so `LOGGEN035` and Microsoft redaction recognise
  Ark-classified members with no bridge. Each carries `Notes` (purpose of
  processing) and is valid on property, field, parameter, and type.
- **Escape hatches**: `[ComplianceReviewed(string diagnosticId, string reason)]`
  with an optional `Expires` date (the shape used in PRD §6.8), and
  `[NotPersonalData(string justification)]` for a member the lexicon flags but
  that genuinely holds no personal data. Both require a non-empty reason;
  `ARKPII008`/`ARKPII009` (PII-IMP-04) enforce that.
- **Purpose gate**: `CompliancePurpose` — the value passed to `Reveal(...)`;
  a closed set of named purposes plus a `Custom(string)` factory that keeps the
  reason greppable.
- **Redactors**: `ArkRedaction` (`Erase`, `Mask`, `Hmac`, `None`) and matching
  `Redactor` implementations. `Hmac` derives its key from configuration and
  fails closed (erases) when no key is configured — a stable pseudonym must
  never silently degrade to cleartext.
- **No analyzer, no generator, no NLog reference** in this package; it must be
  referenceable from a domain assembly with no infrastructure pull-in.

## Implementation steps

1. Create the project, wire it into `Ark.Tools.slnx`, add package metadata
   matching sibling packages.
2. Implement the attribute set over `DataClassificationAttribute`, with XML
   documentation on every public member stating the risk being described.
3. Implement `CompliancePurpose`, `ArkRedaction`, and the redactor set,
   including the fail-closed HMAC key path.
4. Add `ArkComplianceTaxonomy` exposing the `DataClassification` constants so
   downstream generators and analyzers resolve them by symbol, not by name.
5. Update `Directory.Packages.props` and all `packages.lock.json`.

## Required test coverage

- Every attribute reports the expected `DataClassification` and survives a
  round-trip through `Microsoft.Extensions.Compliance` redaction APIs.
- `Hmac` with no configured key erases instead of returning cleartext.
- `Mask` never emits any character of the input; `Erase` output length does not
  vary with input length.
- `[ComplianceReviewed]` and `[NotPersonalData]` with an empty reason fail
  validation; an expired `Expires` date is representable and readable.
- The package's transitive closure contains no logging or ASP.NET dependency.

## Outcomes

- One classification vocabulary shared by Ark and the Microsoft stack.
- A redaction primitive with fail-closed defaults that later layers reuse.

## Acceptance

- [x] `Ark.Tools.Compliance` ships the classification attributes, both escape
  hatches, purposes, and redactors.
- [x] Attributes derive from `DataClassificationAttribute`.
- [x] Redaction fails closed with no configuration.
- [x] Lock files are regenerated for the new dependency.
- [x] The [task board](../README.md) status for PII-IMP-01 matches this task.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
