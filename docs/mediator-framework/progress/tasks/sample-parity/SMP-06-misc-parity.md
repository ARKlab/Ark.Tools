# SMP-06 — Misc parity: App Insights, config layering, IClock/NodaTime, richer test infra (G9)

**Category**: sample-parity · **Priority**: Release blocker · **Scope**: SAMPLE

## Problem

Remaining ReferenceProject parity items missing from the mediator sample (analysis rows 11, 14–16):

1. **Application Insights** integration (ReferenceProject wires `Ark.Tools.ApplicationInsights.HostedService` / AspNetCore AI packages).
2. **Configuration layering** (KeyVault/environment config sources as in ReferenceProject host setup).
3. **`IClock`/NodaTime demo**: handlers using injected `IClock` instead of `DateTime.UtcNow`, with NodaTime types on a contract (already partially covered by the NodaTime protobuf/Json support).
4. **Richer test infra**: mock/fake clock in tests, docker service management in hooks, broader scenario coverage.

This task may be split into up to 4 PRs (one per item) if any grows; each sub-item is independently mergeable.

## Steps

1. **App Insights**: mirror the ReferenceProject registration (search `ApplicationInsights` in
   `samples/Ark.ReferenceProject/Core/Ark.Reference.Core.Application/Host/ApiHost.cs` and WebInterface
   startup); no-op when no connection string configured; document the setting in the sample README.
2. **Config layering**: mirror ReferenceProject `Program.cs`/host builder config sources (KeyVault
   optional, env-specific appsettings). Keep the sample runnable with zero external services.
3. **IClock**: register NodaTime `SystemClock`/`FakeClock` (tests) via SimpleInjector as in
   ReferenceProject; use it for greeting timestamps; add a NodaTime property (`Instant`/`LocalDate`)
   to a contract and verify JSON/MessagePack/protobuf round-trip (NodaTime support packages already exist).
4. **Test infra**: `FakeClock` injection in Reqnroll hooks (`Hooks/SampleTestContext.cs`), time-travel
   step definitions if ReferenceProject has them; docker compose management notes.
5. Tests: NodaTime round-trip parity test across the three wire formats; clock-dependent scenario
   using `FakeClock` deterministic assertions.

## Outcomes

- Mediator sample reaches operational parity with ReferenceProject on telemetry, configuration, time handling and test ergonomics.

## Acceptance

- [x] AI wiring present and inert without configuration (`ApplicationInsightsTests.cs:26-36` inertness test; wired in `Program.cs:34/37` and `WebInterface/SampleHost.cs:50`).
- [x] Config layering matches ReferenceProject shape; sample still runs standalone (optional KeyVault layering `SampleHost.cs:44-48`).
- [ ] `IClock` used by handlers; `FakeClock` in tests; NodaTime contract round-trips JSON/MessagePack/protobuf (tests). (`IClock` registered `ApplicationComposition.cs:200` and injected in handlers; `FakeClock` in `ApplicationTestContext.cs:57,106`; NodaTime `Instant` used on contracts — but no test round-trips a NodaTime member across JSON + MessagePack + protobuf)
- [ ] Full solution build + tests green. (NodaTime round-trip coverage above is missing; not asserting the overall gate for this task)
