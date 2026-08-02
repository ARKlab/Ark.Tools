# HST-07 — Complete classic Application Insights defaults

**Category**: aspnetcore · **Scope**: FRAMEWORK + SAMPLE
**Depends on**: HSD-07, HSD-09

## Problem

The sample has partial classic Application Insights registration and omits Ark
telemetry initializers, processors and application-container integration.

## Steps

1. Complete the existing classic Ark setup without duplicate registrations:
   user telemetry, 4xx-as-success behavior, processors, dependency settings,
   adaptive sampling and SimpleInjector integration.
2. Skip Snapshot Debugger only.
3. Preserve the convention that 4xx responses are not server failures.
4. Apply existing privacy filtering before exporting approved user identity.
5. Test requests, 4xx, 5xx, dependencies and identity with an in-memory channel;
   never transmit telemetry from tests.

## Outcomes

- The optional Ark defaults provide the complete current telemetry baseline except
  Snapshot Debugger.

## Acceptance

- [ ] No duplicate request or dependency telemetry is emitted.
- [ ] 4xx requests are not classified as server failures; 5xx requests are.
- [ ] Snapshot Debugger is not registered.
- [ ] The sample remains inert without telemetry configuration.
- [ ] Full solution build and tests pass with zero warnings.
