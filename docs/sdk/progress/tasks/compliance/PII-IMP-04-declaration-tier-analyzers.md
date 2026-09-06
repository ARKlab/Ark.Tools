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
- **`ARKPII008`** (warning): `[ComplianceReviewed]` lacks a reason or its
  `Expires` date has passed — a suppression nobody can justify is a suppression
  nobody reviewed.
- **`ARKPII009`** (warning): `[NotPersonalData]` justification is missing or
  boilerplate, so opting out of classification stays a deliberate, readable act.
- **`ARKPII010`** (error): a classification attribute sits on a member the
  pipeline cannot redact — open `object`, `dynamic`, a delegate, or a
  `[ValueObject<T>]` (Vogen) type that still has a cleartext leak surface
  enabled (`DebuggerDisplay`, a cleartext `TypeConverter.ConvertTo`, an implicit
  conversion, or a generated `ToString`). For the Vogen case the message names
  the exact option to change; see PRD [§14.5](../../../privacy-by-default-prd.md#145-interop-not-exclusion).
- **Lexicon**: `ComplianceLexicon.Ark.txt` as an `AdditionalFiles` input,
  packaged with a default list and composable with consumer entries, following
  the `BannedSymbols.Ark.txt` precedent.
- **Code fixes**: add the classification attribute; add a `[NotPersonalData]`
  justification; convert a classified `string` property to the matching built-in
  sensitive value object.
- **Not this tier**: `ARKPII002/003/004/005/011` need expression flow and belong
  to PII-IMP-05; `ARKPII007/012` are PII-IMP-08; `ARKPII013` is PII-IMP-10. The
  ID list here and the PRD §7 table are the same list, deliberately.

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

- One positive and one negative case per rule, plus a case proving the severity
  of each rule matches the PRD §7 table (`ARKPII001/008/009` warning,
  `ARKPII010` error).
- Consumer lexicon entries add and remove terms.
- Code fixes produce compiling output, including the value-object conversion.
- Analyzer throughput on the reference project stays inside the documented
  budget.

## Outcomes

- Undeclared PII is visible at the point of declaration.
- Opting out of classification, and suppressing a rule, both require a reason.
- Vogen-based value objects are usable without becoming a leak.

## Acceptance

- [x] `ARKPII001/008/009/010` are implemented with the PRD §7 severities.
- [x] The lexicon is a composable `AdditionalFiles` input.
- [x] Every rule has a code fix where a mechanical fix exists.
- [x] The [task board](../README.md) status for PII-IMP-04 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.

## Implementation evidence (2026-09-06)

- Added `Ark.Tools.Compliance.Analyzers` and its separately loaded
  `Ark.Tools.Compliance.Analyzers.CodeFixes` assembly. Compiler analyzers do not
  reference Roslyn Workspaces; the package carries both DLLs as analyzer assets.
- Declaration checks cover fields, properties, method/indexer parameters,
  positional records, type-level classification and classification-attribute
  inheritance. Vogen diagnostics identify unsafe conversion, debugger and
  generated formatting options; Vogen versions without a safe debugger option
  require an Ark sensitive value object.
- Embedded default lexicon entries are composable with consumer
  `ComplianceLexicon*.txt` inputs. Identifier-word matching supports negative
  terms and explicit prefix wildcards; parsed consumer snapshots are weakly
  cached by `SourceText`.
- Code actions add classification or a deliberate, still-diagnosed exclusion
  placeholder. Value-object conversion preserves nullable/default initializers
  and is withheld for existing callers and interface/override contracts that
  would otherwise break. An additional refactoring handles already-classified
  string properties. Batch Fix All is exposed for diagnostic fixes.
- Focused analyzer/test builds succeeded with zero warnings/errors; the
  combined declaration, fixture, sink and SQL analyzer test project passed
  153 tests, including the final classified-property refactoring test.
  Code-fix output is compiled by the tests rather than compared only as text.
  Standalone `dotnet pack` succeeded; analyzer DLLs, defaults and transitive
  props were inspected in the resulting package.
- CI's inherited coverage instrumentation caused `BadImageFormatException`;
  running the built test module directly with
  `dotnet test --test-modules tests/Ark.Tools.Compliance.Analyzers.Tests/bin/Debug/net10.0/Ark.Tools.Compliance.Analyzers.Tests.dll --minimum-expected-tests 1`
  passed without coverage instrumentation.
- Remaining integration evidence: reference-project throughput budget, shared
  task-board reconciliation and the parent's final full-solution build/test.
  These checks are intentionally not represented as complete above.
