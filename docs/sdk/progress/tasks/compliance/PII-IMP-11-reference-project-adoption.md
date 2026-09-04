# PII-IMP-11 — ReferenceProject adoption and end-to-end proof

**Category**: compliance-migration · **Priority**: medium
**Depends on**: PII-IMP-03, PII-IMP-08, PII-IMP-09, PII-IMP-10
**Scope**: SAMPLE MIGRATION + E2E TESTS
**Design**: [Testing](../../../privacy-by-default-prd.md#11-testing),
[Success criteria](../../../privacy-by-default-prd.md#12-success-criteria)

## Problem

Until the whole stack runs on a real service, the design is a document. The
reference project is where the developer experience of §6 is either pleasant or
obviously wrong, and where the layers are proven to compose.

## Execution map

- Classify the reference domain, convert the string-shaped members to sensitive
  value objects, and add `[SqlDataPolicy]`/`[SqlColumnPolicy]` to the persisted
  entities.
- Apply the generated SQL template to the reference database, including the
  masking and sensitivity classification statements.
- Commit the generated `ArkComplianceSurface.txt` baselines.
- Replace any real-looking fixture data with reserved fakes.
- Add end-to-end tests spanning HTTP → handler → SQL → log → OTel.

## Implementation steps

1. Migrate the domain and DTOs, fixing every diagnostic rather than suppressing
   it; each remaining suppression carries a `[ComplianceReviewed]` justification.
2. Apply the SQL template through the existing database deployment path.
3. Regenerate and commit surface baselines.
4. Add the e2e tests and an AoT publish smoke test.

## Required test coverage

- A request carrying personal data produces logs and spans where the value is
  masked, with no redaction call in the test setup.
- The API response and OpenAPI document remain correct: cleartext where the
  contract requires it, primitive schema, `x-ark-classification` present.
- The database column is masked for a low-privilege reader and readable for the
  privileged one.
- The committed compliance surface matches the build.
- An AoT publish of the reference API succeeds with no trim warnings from the
  compliance packages.

## Outcomes

- A worked example of the whole design that developers can copy.
- Proof that the five layers compose without contradiction.

## Acceptance

- [ ] The reference project is fully classified with justified suppressions
  only.
- [ ] End-to-end masking is proven across log, span, and database.
- [ ] The OpenAPI contract is proven correct for sensitive value objects.
- [ ] AoT publish is clean.
- [ ] The [task board](../README.md) status for PII-IMP-11 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
