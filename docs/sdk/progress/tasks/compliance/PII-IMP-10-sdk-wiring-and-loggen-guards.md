# PII-IMP-10 — SDK wiring, LOGGEN guards, and documentation

**Category**: compliance-sdk · **Priority**: high
**Depends on**: PII-IMP-04, PII-IMP-05, PII-IMP-06, PII-IMP-07
**Scope**: PACKAGED CONFIGURATION + BUILD TARGETS + DOCS + TESTS
**Design**: [Packaging](../../../privacy-by-default-prd.md#9-packaging),
[Rollout](../../../privacy-by-default-prd.md#10-rollout),
[LOGGEN overlap](../../../privacy-by-default-prd.md#133-rejected-migrating-ark-logging-to-loggermessage--loggen-wholesale)

## Problem

Rules that each project must opt into are rules most projects will not have.
The SDK is where the default becomes the default — and where the Microsoft
logging path, which is mandatory inside Microsoft stack extension points, gets
the same guarantees as the NLog path.

## Execution map

- **`ArkComplianceMode`**: `Enforce` (default, §7 severities from day one) or
  `Off` via `EnableArkToolsCompliance=false`. There is no Observe stage: most
  code here is written by agents outside an IDE, and a `suggestion` that does not
  fail `dotnet build` is a diagnostic nobody reads. Per-rule `.editorconfig`
  overrides remain available.
- **Packaged config**: `Ark.Tools.Compliance.globalconfig` shipped by
  `Ark.Tools.Build` on the same level and switch pattern as the existing
  analyzer assets, inert when the analyzer package is absent.
- **LOGGEN escalation**: `LOGGEN035`/`LOGGEN017`/`LOGGEN026` = error and
  `LOGGEN036` = warning in the packaged config. Free for projects that never
  reference `Microsoft.Extensions.Telemetry.Abstractions`, since the generator is
  not present.
- **`AddArkRedaction()`**: performs both `AddRedaction()` and `EnableRedaction()`
  so the half-configured, silently-unredacted state cannot occur.
- **`ARKPII013`**: the project references the Microsoft telemetry logging stack
  without `AddArkRedaction()`.
- **Docs**: `docs/analyzers.md` rule table, adoption guidance, and the rule that
  inside Microsoft stack extension points (middleware, filters, `IHostedService`,
  EF Core interceptors, Azure Functions middleware) the injected `ILogger<T>` is
  used, not NLog — `NLogConfigurer` already bridges it.

## Implementation steps

1. Add the packaged global config and its `EnableArkToolsCompliance` switch.
2. Implement `ArkComplianceMode` and per-rule override composition.
3. Implement `AddArkRedaction()` and `ARKPII013`.
4. Extend `docs/analyzers.md` and the SDK adoption docs.

## Required test coverage

- A clean-consumer fixture gets every `ARKPII*` rule at its accepted severity
  with no project configuration.
- `EnableArkToolsCompliance=false` disables all of them and nothing else.
- A local `.editorconfig` lowers one rule without affecting the rest.
- The config is inert when the analyzer package is absent.
- `LOGGEN035` breaks the build for an Ark-classified member on a
  `[LoggerMessage]` method, proving vocabulary compatibility with no bridge.
- `ARKPII013` fires without `AddArkRedaction()` and clears with it.

## Outcomes

- Enforcement is the default for every consumer of the SDK.
- The NLog and Microsoft logging paths have equivalent guarantees.

## Acceptance

- [ ] `ArkComplianceMode` has exactly two states and one documented opt-out.
- [ ] The packaged config is inert without the analyzer package.
- [ ] LOGGEN guards are escalated and proven against Ark-classified types.
- [ ] `AddArkRedaction()` and `ARKPII013` remove the half-configured state.
- [ ] Documentation covers the rules and the Microsoft-logging boundary rule.
- [ ] The [task board](../README.md) status for PII-IMP-10 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
