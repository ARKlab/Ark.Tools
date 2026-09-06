# PII-IMP-05 — Sink-tier analyzers

**Category**: compliance-analyzer · **Priority**: high
**Depends on**: PII-IMP-04
**Scope**: ANALYZER RULES + CONFIGURATION ASSET + TESTS
**Design**: [Logging](../../../privacy-by-default-prd.md#63-logging),
[Exceptions](../../../privacy-by-default-prd.md#64-exceptions-and-error-contracts),
[Analyzer implementation strategy](../../../privacy-by-default-prd.md#8-analyzer-implementation-strategy)

## Problem

This is the rule set the PRD exists for: classified data reaching a log
template, an exception message, or another unbounded sink. No analyzer in the
ecosystem checks exception messages at all, and `LOGGEN035` only covers
`[LoggerMessage]`, which Ark code does not use.

## Execution map

- **`ARKPII002`**: classified data used as a structured-log argument or in a log
  message — NLog `Logger.*`, `ILogger.Log*`, `BeginScope`, and any method listed
  in `ComplianceSinks.Ark.txt`.
- **`ARKPII003`**: classified data in an exception message or in
  `ArgumentException.paramName`-adjacent message text.
- **`ARKPII004`**: classified data reaching an `Activity` tag, a metric
  dimension, or baggage — telemetry is a sink like any other, and one that
  usually leaves the trust boundary.
- **`ARKPII005`**: implicit `ToString`, interpolation, concatenation, or a
  `Reveal` with no purpose applied to a classified value.
- **`ARKPII011`**: classified data passed to a banned formatting sink —
  `Console.*`, `Debug.*`, `Trace.*`, `StringBuilder.Append`.
- **Scope check**: this list and the PRD §7 table are the same list. The
  declaration-tier rules (`ARKPII001/008/009/010`) are PII-IMP-04.
- **Flow**: intra-method `IOperation` reachability only — locals, interpolated
  strings, `string.Concat`/`Format`, ternaries, and member access chains.
  Explicitly not a taint engine (§13.1); cross-method flow is out of scope by
  design and the diagnostic messages say so.
- **Recursion fix over `LOGGEN035`**: a classified member reached through any
  containing type is reported, not only through records.
- **Sinks**: `ComplianceSinks.Ark.txt` as a composable `AdditionalFiles` input, so a
  consumer can register its own sink methods without an Ark release.

## Implementation steps

1. Implement the `IOperation`-based reachability walk with an explicit depth and
   node budget, bailing out to no-diagnostic rather than hanging.
2. Implement the five rules on top of it.
3. Implement the sinks file reader, defaulting to NLog, `ILogger`,
   `Console`/`Debug`/`Trace`, and exception constructors.
4. Write diagnostic messages that name the member, the classification, and the
   safe alternative (log the key, not the person).

## Required test coverage

- Positive/negative pairs for each rule, including interpolation, `string.Format`,
  nested member access, collection element access, and a ternary.
- A classified member two levels deep inside a non-record class is reported.
- Consumer-registered sinks are honoured; removing a default sink is possible.
- A pragma and a `[ComplianceReviewed]` justification both suppress cleanly.
- A pathological expression tree hits the budget and produces no diagnostic
  rather than timing out.

## Outcomes

- The leak paths the PRD opens with become build failures.
- Exception messages are covered, which nothing in the ecosystem does today.

## Acceptance

- [x] `ARKPII002/003/004/005/011` are implemented over intra-method
  `IOperation` flow, with the PRD §7 meanings and severities.
- [x] Non-record containing types are traversed.
- [x] `ComplianceSinks.Ark.txt` composes with consumer entries.
- [x] The [task board](../README.md) status for PII-IMP-05 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.

## Implementation notes

- `SinkTaintAnalyzer` reports all five rules as errors. Coverage includes NLog and
  Microsoft `ILogger` scopes/extensions, exception messages and data keys/values,
  `BusinessRuleViolation` members, Activity events/tags/baggage, metric dimensions,
  and the formatting families listed above.
- The backward operation walk follows local declarations/assignments, conditional
  alternatives, nullable member access, casts, string formatting, collection
  initializers, and local collection writes. Unconditional local overwrites stop
  earlier flow; conditional and loop-carried writes conservatively retain possible sources.
  Cross-method calls and captured-variable flow across function boundaries are
  deliberately not followed. This is bounded reachability, not a control-flow or
  inter-procedural taint engine.
- Classification includes custom attributes derived from Microsoft's classification
  base, positional record parameters, inherited/non-record containing types, and
  sensitive-value contracts, including generic constraints. Pseudonymous values and
  explicit redactor results are permitted.
- `ComplianceSinks*.txt` files use `M:Documentation.Comment.Id;kind`, where `kind`
  is `log`, `exception`, `telemetry`, or `format`. A trailing `*` matches method
  prefixes; a leading `-` removes a sink. Consumer files apply after
  `ComplianceSinks.Ark.txt`, in ordinal path order, so removal is independent of
  `AdditionalFiles` enumeration order. Empty lines and `#`/`//` comments are ignored.
- Analyzer options `ark_compliance.max_type_depth` (default 5, maximum 32),
  `ark_compliance.max_operation_depth` (default 64, maximum 128), and
  `ark_compliance.max_operation_nodes` (default 512, maximum 4096) bound traversal.
  Exhausted operation budgets produce no diagnostic.
- Focused validation uses
  `dotnet build tests/Ark.Tools.Compliance.Analyzers.Tests/Ark.Tools.Compliance.Analyzers.Tests.csproj --no-restore --configuration Debug`
  and
  `dotnet test --project tests/Ark.Tools.Compliance.Analyzers.Tests/Ark.Tools.Compliance.Analyzers.Tests.csproj --no-build --configuration Debug --filter 'FullyQualifiedName~SinkTaintAnalyzerTests'`.
  Latest focused result: build succeeded with zero warnings/errors; 93 tests
  passed, zero failed or skipped.
  Full-solution acceptance remains unchecked until independently verified.
