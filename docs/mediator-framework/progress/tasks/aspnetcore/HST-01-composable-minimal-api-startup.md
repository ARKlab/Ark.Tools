# HST-01 — Composable Minimal API startup

**Category**: aspnetcore · **Scope**: FRAMEWORK + SAMPLE
**Depends on**: HSD-01, HSD-02

## Problem

Minimal API consumers have no reusable Ark composition contract. Full
SimpleInjector verification is coupled to the sample's Rebus hosted service.

## Steps

1. Add the accepted composable service, application-builder and container hooks.
2. Include the established secure authentication/authorization defaults and
   document middleware and endpoint order.
3. Build and run `Container.Verify()` after all registrations and cross-wiring,
   but before the host accepts any request. Do not inspect or start external
   services.
4. Keep generated missing-handler diagnostics.
5. Move Rebus startup out of framework verification. In the sample, demonstrate
   that the one-way bus starts during host startup before requests can resolve
   `IBus`.
6. Test invalid registrations, cross-wiring, decorators, a host without Rebus,
   and Rebus availability on the first request.

## Outcomes

- Optional Ark defaults provide one reusable Minimal API composition contract.
- Invalid containers fail during startup; Rebus lifecycle remains application
  composition.

## Acceptance

- [x] Authentication and authorization defaults match the existing Ark baseline.
- [x] Verification completes before any request and does not probe external services.
- [x] The sample starts Rebus before serving requests.
- [x] Public APIs have XML documentation and the design/guide is updated.
- [ ] Full solution build and tests pass with zero warnings.
