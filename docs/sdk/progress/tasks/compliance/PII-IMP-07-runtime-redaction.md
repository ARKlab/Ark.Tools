# PII-IMP-07 — Runtime redaction: NLog pipeline and OTel processor

**Category**: compliance-runtime · **Priority**: high
**Depends on**: PII-IMP-01
**Scope**: NEW PACKAGE + NLogConfigurer WIRING + TESTS
**Design**: [Runtime redaction](../../../privacy-by-default-prd.md#69-runtime-redaction-second-net),
[Decision PII‑06](../../../privacy-by-default-prd.md#17-decisions)

## Problem

Analyzers cannot see third-party types, dynamic payloads, or data that arrives
as `object`. The runtime net catches what compile time missed — and a redaction
you have to remember to switch on is a redaction that leaks, so it must be on by
default.

## Execution map

- **Package**: `Ark.Tools.Compliance.NLog`, built on NLog's real extension
  points — `RegisterObjectTransformation`, `RegisterValueFormatter`, and a
  `WrapperTargetBase`. NLog has no `ILogEventInterceptor`, contrary to common
  claims; the design must not assume one.
- **On by default**: `NLogConfigurer.WithArkDefaultTargetsAndRules` — and
  therefore `WithDefaultTargetsAndRulesFromConfiguration` and
  `IHostBuilder.ConfigureNLog` — wires redaction whenever the package is
  referenced. Defaults with no call: `Default = Erase`, `PersonalData = Hmac`,
  `SensitivePersonalData = Erase`, `Secret = Erase`, `Pseudonymous = None`,
  `PatternScan = Off`.
- **`WithComplianceRedaction(...)`** overrides those defaults;
  **`WithoutComplianceRedaction()`** is the explicit, greppable opt-out.
- **Pattern scan** (decision PII‑06): ships, default off, optimised matching over
  a compiled pattern set for values that arrive untyped.
- **OTel**: a redaction processor in the shape of the existing
  `ArkPreFilterProcessor`, registered by the default setup.

## Implementation steps

1. Implement the transformation/formatter registrations and the wrapper target.
2. Wire the defaults into `NLogConfigurer` behind a reference check so projects
   without the package are unaffected.
3. Implement `WithComplianceRedaction`/`WithoutComplianceRedaction`.
4. Implement the pattern scanner with a compiled, allocation-conscious matcher
   and a documented throughput budget.
5. Implement and register the OTel processor.

## Required test coverage

- An integration test logs a classified value with **no** redaction call in the
  setup and asserts the mask appears in the target output.
- Each classification uses its default redactor; overrides apply.
- `WithoutComplianceRedaction()` restores cleartext, proving the opt-out is real
  and greppable.
- The pattern scanner is off by default and, when enabled, masks a known
  pattern in an untyped payload.
- The OTel processor redacts attributes on exported spans.
- A throughput test bounds the added cost per log event.

## Outcomes

- A second net that is active without any developer action.
- Fail-closed defaults: an unknown classification erases rather than prints.

## Acceptance

- [ ] Redaction is wired by the default NLog setup with no explicit call.
- [ ] Overrides and an explicit opt-out exist and are tested.
- [ ] Pattern scan ships default-off.
- [ ] The OTel processor is registered by the default setup.
- [ ] The [task board](../README.md) status for PII-IMP-07 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
