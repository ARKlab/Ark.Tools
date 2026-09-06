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

- [x] Redaction is wired by the default NLog setup with no explicit call.
- [x] Overrides and an explicit opt-out exist and are tested.
- [x] Pattern scan ships default-off.
- [x] The OTel processor is registered by the default setup.
- [x] The [task board](../README.md) status for PII-IMP-07 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.

## Implementation notes

- `Ark.Tools.Compliance.NLog` registers NLog's object transformation and value
  formatter extensions. Wrappers precede asynchronous targets; a weak event cache
  shares one sanitized event across target rules without changing rule thresholds
  or mutating the caller's event.
- `ComplianceRedactionOptions` and `ComplianceRedactor` live in the foundation so
  OTel does not acquire an NLog dependency. Missing HMAC keys and unknown
  classifications emit the alertable `***ARKPII***` marker. Supply `HmacKey` from
  the application's secret provider to obtain stable personal-data pseudonyms.
- Generated sensitive values implement a reflection-free runtime contract.
  Dynamic DTO inspection is bounded to depth 8, 64 items per collection and 256
  visited values. Getter failures erase. NativeAOT erases opaque dynamic objects;
  generated values retain their typed behavior.
- The shared `PersonalDataPatterns` source supplies the same patterns and
  checksum validation to ARKPII006 and the runtime scanner. Runtime matching uses
  `GeneratedRegex`, a `SearchValues<char>` prefilter and a 25 ms timeout that
  erases on failure. Reserved test values are still redacted at runtime.
- NLog exceptions are conservatively replaced with the marker because exception
  text and `Data` cannot be assumed safe. Explicit opt-out does not undo generated
  sensitive values' safe `ToString()` behavior.
- Default ASP.NET Core, ResourceWatcher and Application Insights tracing setup
  registers the span processor. Custom tracing pipelines must register redaction
  **before exporters**. Span tags are covered; event/link attributes, baggage,
  metrics and independent OpenTelemetry log export are not covered by this task.
- The NuGet package includes a `buildTransitive` startup source to root the
  optional NLog hook in trimmed consumers. Ordinary project references use an
  optional assembly lookup; hosts without this package keep existing behavior.

## Focused validation

The runtime package has built successfully for net8.0 and net10.0; focused NLog,
OTel and Application Insights builds have reported zero warnings. Default
redaction, policy overrides, opt-out, generated values and exported span tags
have passed focused integration tests. Emitted sensitive-value `.g.cs` files
were inspected.

Throughput tests exercise the actual wrapper and a 200-character scanned message.
The CI smoke ceiling is 50 microseconds per operation, allowing shared-runner
contention. The PRD's **2 microseconds** release scanner target still requires
an isolated, warmed release measurement; it is not claimed as satisfied.
Full-solution build/test acceptance remains unchecked.
