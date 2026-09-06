# PII-IMP-09 — Test data rules and reserved-value fakes

**Category**: compliance-testing · **Priority**: medium
**Depends on**: PII-IMP-04
**Scope**: ANALYZER RULE + TEST HELPERS + TESTS
**Design**: [Test data](../../../privacy-by-default-prd.md#67-test-data)

## Problem

Real personal data in fixtures is personal data in the repository, in every
clone, and in every CI log — with none of the controls that protect production.
It is also the easiest leak to prevent, because a fake is always acceptable.

## Execution map

- **`ARKPII006`**: a literal in test source or in a Reqnroll feature table that
  matches a personal-data pattern (email, phone, national identifier, IBAN,
  postal address) and is not drawn from a reserved range.
- **Reserved generator**: RFC 2606 domains (`example.com`, `example.org`),
  reserved phone ranges, and documented invalid-checksum identifiers, exposed as
  `ComplianceFakes` in `Ark.Tools.Reqnroll` and shared with the OpenAPI example
  emitter from PII-IMP-03.
- **Feature files**: analysed through `AdditionalFiles`, so a `.feature` table
  cell is covered even though it is not C#.
- **Code fix**: replace the literal with the matching reserved fake.

## Implementation steps

1. Implement the literal scanner with the pattern set shared with the runtime
   scanner from PII-IMP-07, so a pattern is maintained once.
2. Implement the reserved-value generator with stable, deterministic output per
   seed so tests stay reproducible.
3. Add Reqnroll value retrievers producing sensitive value objects from feature
   tables.
4. Implement the code fix.

## Required test coverage

- A realistic email, phone number, and national identifier in test source are
  each reported; the reserved equivalents are not.
- A `.feature` table cell is reported.
- The generator is deterministic for a given seed and never emits a routable
  address or allocatable number.
- The code fix produces compiling, still-passing tests.

## Outcomes

- Fixtures stop being an uncontrolled copy of production data.
- One pattern set serves the analyzer, the runtime scanner, and the fakes.

## Acceptance

- [x] `ARKPII006` covers C# literals and Reqnroll feature tables.
- [x] Reserved-value fakes ship and are shared with OpenAPI examples.
- [x] A code fix replaces a flagged literal.
- [x] The [task board](../README.md) status for PII-IMP-09 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.

## Implementation evidence (2026-09-06)

- Added `TestDataComplianceAnalyzer` (`ARKPII006`, warning). It identifies test
  compilations through `IsTestProject`, test-framework references and test
  source paths; production literals are not scanned. Reqnroll table cells are
  checked through Roslyn's additional-file action with precise file spans.
- `Ark.Tools.Compliance/Shared/PersonalDataPatterns.cs` is linked into the
  analyzer and consumed by the runtime scanner. The dependency-free source
  owns email, phone, national-identifier, IBAN and postal-address patterns,
  reserved-value recognition, SSN/tax-code validation and IBAN mod-97 checks.
  Country-length bounds keep formatted IBAN matching from swallowing
  surrounding prose.
- Reused the existing deterministic `Ark.Tools.Compliance.ComplianceFakes`
  API, already consumed by OpenAPI examples, and the existing
  `Ark.Tools.Compliance.Reqnroll` value retriever/comparer rather than adding a
  second fake generator or a new dependency. Analyzer tests compile the actual
  fake-generator source and cover negative and extreme integer seeds.
- The C# code fix replaces all detected spans in one literal while retaining
  surrounding text. Tests compile its output and run the analyzer again to
  prove the reserved replacements are silent. Feature-file locations are
  reported; editing non-C# documents remains a manual replacement.
- Focused builds succeeded with zero warnings/errors and the combined analyzer
  test project passed 153 tests, including plausible/negative cases for every
  supported pattern, reserved lookalikes, deterministic fakes and feature-cell
  locations. Full-solution and board acceptance remain for the parent.
