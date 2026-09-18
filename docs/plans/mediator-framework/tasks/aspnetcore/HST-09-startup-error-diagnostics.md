# HST-09 — Startup-error diagnostics

**Category**: aspnetcore · **Scope**: FRAMEWORK + SAMPLE
**Depends on**: HSD-01, HSD-10

## Problem

Minimal API consumers lack the Ark startup-error capture used to diagnose grave
failures during smoke tests.

## Steps

1. Add `CaptureStartupErrors(true)` and detailed startup errors to the optional
   Ark host defaults.
2. Keep this behavior independent from structured fatal logging and process exit.
3. Test failures during service configuration, host build and application startup.
4. Document the diagnostic exposure and that direct host composition remains possible.

## Outcomes

- Smoke tests retain actionable diagnostics for failures before request handling.

## Acceptance

- [x] Startup failures are visible through the hosting diagnostic path.
- [x] The same failure is logged and still fails process startup.
- [x] Applications can omit the Ark defaults by composing the host directly.
- [x] Full solution build and tests pass with zero warnings.
