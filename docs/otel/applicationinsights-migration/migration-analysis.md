# Current migration analysis

Updated: 2026-08-15

## Scope

The supported migration surface is the Ark.Tools extension API, not the removed
Application Insights v2 processor and sampling APIs.

## Interface mapping

| Existing Ark.Tools extension | Action |
|---|---|
| `ArkApplicationInsightsTelemetry` | Keep; update packages and enable profiler |
| `AddApplicationInsightsForHostedService` | Keep; update packages and enable profiler |
| `AddArkApplicationInsightsCustomizations` | Keep; configure the OTel sampler and processors |
| Rebus `UseApplicationInsightMetrics` | Keep; validate metric names and dimensions |

## Sampling decision

The adaptive sampler is a parent-sampling enhancement, not an independent child sampler.
It uses adaptive budgets only for roots. Parent decisions preserve chain continuity, while
record-only children remain eligible for failure promotion. This avoids both split traces and
unbounded retention.

## Operational risks

- The profiler package is prerelease and experimental.
- Dynamic operation names can consume bucket capacity; retain the configured cap.
- Failure promotion cannot recover siblings that ended before the failure was observed.
- 4xx normalization must run before the failure promotion processor.

## Validation

Validate one root-only trace, one sampled parent chain, one unsampled parent chain, one 4xx,
one 5xx, one exception, and successful and failed Rebus processing.
