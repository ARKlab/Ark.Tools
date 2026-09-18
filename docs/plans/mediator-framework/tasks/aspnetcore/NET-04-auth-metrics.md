# NET-04 — Auth + Identity metrics in the sample (N8)

**Category**: aspnetcore · **Priority**: Post-release · **Scope**: SAMPLE
**Depends on**: pairs naturally with SMP-06 (App Insights).

## Problem

.NET 10 emits authentication/authorization metrics
(`Microsoft.AspNetCore.Authentication`/`Authorization` meters: authenticated-request duration,
challenges, forbids, authorization results). The sample neither collects nor documents them.

## Steps

1. Register OpenTelemetry metrics (or the App Insights equivalent from SMP-06) collecting the
   `Microsoft.AspNetCore.Authentication` and `Microsoft.AspNetCore.Authorization` meters in
   `SampleStartup.cs`/`Program.cs`. Prefer whatever telemetry stack SMP-06 lands — don't introduce a
   parallel one; if SMP-06 not merged, use `AddOpenTelemetry().WithMetrics(...)` only if the packages
   are already available centrally, else document-only.
2. Document the emitted metric names and how to observe them (sample README section).
3. Test: meter-listener test asserting the authenticate-duration metric is emitted for a request
   (use `MetricCollector<T>` from `Microsoft.Extensions.Diagnostics.Testing` if already referenced;
   otherwise a lightweight `MeterListener`).

## Outcomes

- Auth/authz metrics observable out of the box in the sample and documented for consumers.

## Acceptance

- [ ] Metrics collected via the sample's telemetry stack (no new dependency without approval).
- [ ] README documents meters and dashboards/queries.
- [ ] Metric emission covered by a test.
- [ ] Full solution build + tests green.
