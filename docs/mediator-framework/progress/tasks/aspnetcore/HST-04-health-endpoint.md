# HST-04 — Default health endpoint

**Category**: aspnetcore · **Scope**: FRAMEWORK + SAMPLE
**Depends on**: HSD-02, HSD-05, HSD-09

## Problem

The Minimal API defaults omit the established Ark `/healthCheck` contract.

## Steps

1. Register and map the existing `/healthCheck` endpoint contract in the optional
   Ark defaults.
2. Do not register or expose HealthChecks UI, history storage or UI assets by
   default.
3. Keep container verification as a startup failure rather than a health check.
4. Demonstrate optional checks backed by local SQL Server or Azurite containers;
   require no live Azure or externally hosted database.
5. Test healthy, dependency-failed and disclosure-safe responses.

## Outcomes

- Ark Minimal API hosts retain deployment-compatible health behavior without a UI.

## Acceptance

- [x] Path and response shape preserve the existing `/healthCheck` contract.
- [x] Default registration contains no UI or history support.
- [x] Responses disclose no secrets or exception details.
- [ ] Full solution build and tests pass with zero warnings.
