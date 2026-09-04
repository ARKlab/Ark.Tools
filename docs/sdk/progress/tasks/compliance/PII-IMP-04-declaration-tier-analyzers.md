# PII-IMP-04 — Declaration-tier analyzers and code fixes

**Category**: compliance-analyzer · **Priority**: high
**Depends on**: PII-IMP-01
**Scope**: ANALYZER PACKAGE + CODE FIXES + TESTS
**Design**: [Declaring personal data](../../../privacy-by-default-prd.md#61-declaring-personal-data),
[Diagnostics](../../../privacy-by-default-prd.md#7-diagnostics),
[Analyzer implementation strategy](../../../privacy-by-default-prd.md#8-analyzer-implementation-strategy)

## Problem

Enforcement at sinks only works if the data is classified, and classification
only happens if the compiler asks for it. This tier is what turns "we should
have annotated that" into a build outcome.

## Execution map

- **Package**: `Ark.Tools.Compliance.Analyzers`, symbol-tier rules over
  `RegisterSymbolAction`, no syntax walking.
- **`ARKPII001`** (warning, decision PII‑02): a member whose name matches the
  PII lexicon is not classified. It is the only heuristic rule, so it must not
  break a build on a false positive.
- **`ARKPII005`**: a classified member with no `Notes`/purpose.
- **`ARKPII009`**: a classified `string`-typed member that could be a sensitive
  value object.
- **`ARKPII010`**: a `[ValueObject<T>]` (Vogen) type carrying an Ark
  classification while leaving a cleartext leak surface enabled —
  `DebuggerDisplay`, a cleartext `TypeConverter.ConvertTo`, an implicit
  conversion, or a generated `ToString`; the message names the exact Vogen
  option to change.
- **Lexicon**: `ComplianceLexicon.txt` as an `AdditionalFiles` input, packaged
  with a default list and composable with consumer entries, following the
  `BannedSymbols.Ark.Tools.txt` precedent.
- **Code fixes**: add the classification attribute; add `Notes`; convert a
  classified `string` property to the matching built-in value object.

## Implementation steps

1. Create the analyzer project and package it as an analyzer asset.
2. Implement the four rules with `DiagnosticDescriptor` messages that state the
   risk, not just the violation.
3. Implement the lexicon reader with caching keyed on the additional-file
   snapshot, and support negative entries so a consumer can silence a term
   globally rather than per site.
4. Implement the three code fixes with `FixAllProvider` support.
5. Add per-rule severity entries to the packaged global config (wired in
   PII-IMP-10).

## Required test coverage

- One positive and one negative case per rule, plus a case proving `ARKPII001`
  is a warning and the others are errors.
- Consumer lexicon entries add and remove terms.
- Code fixes produce compiling output, including the value-object conversion.
- Analyzer throughput on the reference project stays inside the documented
  budget.

## Outcomes

- Undeclared PII is visible at the point of declaration.
- Vogen-based value objects are usable without becoming a leak.

## Acceptance

- [ ] `ARKPII001/005/009/010` are implemented with the accepted severities.
- [ ] The lexicon is a composable `AdditionalFiles` input.
- [ ] Every rule has a code fix where a mechanical fix exists.
- [ ] The [task board](../README.md) status for PII-IMP-04 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
