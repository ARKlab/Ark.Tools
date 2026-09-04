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

- [ ] `ARKPII002/003/004/005/011` are implemented over intra-method
  `IOperation` flow, with the PRD §7 meanings and severities.
- [ ] Non-record containing types are traversed.
- [ ] `ComplianceSinks.Ark.txt` composes with consumer entries.
- [ ] The [task board](../README.md) status for PII-IMP-05 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
