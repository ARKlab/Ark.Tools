# HST-08 — Production composition-root tests

**Category**: aspnetcore · **Scope**: SAMPLE
**Depends on**: HSD-09, HST-01, HST-06, HST-07

## Problem

Current transport tests bypass production `Program` wiring.

## Steps

1. Extract only the seam required to execute production host registration from tests.
2. Replace credentials and external providers so tests never contact Key Vault,
   Azure Monitor or production logging targets.
3. Assert production registration for NLog, telemetry, security defaults,
   SimpleInjector verification and health.
4. Add a process-level check only where exit/flush behavior cannot be proven in process.
5. Keep existing HTTP, gRPC and Rebus scenarios on the same startup composition.

## Outcomes

- Production and test-host wiring cannot silently diverge.

## Acceptance

- [ ] Tests execute the production composition root without live external services.
- [ ] Local SQL Server or Azurite containers remain supported.
- [ ] Missing or duplicate operational registrations fail a runnable check.
- [ ] Full solution build and tests pass with zero warnings.
