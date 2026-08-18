# Application Insights and OpenTelemetry refresh

Updated: 2026-08-15

## Decision

Keep the current Application Insights 3.x interface and use its OpenTelemetry pipeline.
`ArkAdaptiveSampler` remains the Ark.Tools enhancement because the troubleshooting
requirements need failure promotion and explicit parent-chain behavior.

## Current baseline

- Application Insights SDK packages: 3.1.2
- OpenTelemetry packages: 1.17.0
- Azure Monitor OpenTelemetry Profiler: 1.0.1-beta.7
- Ark setup extensions: ASP.NET Core and hosted service

## Why this remains the right design

| Concern | Current design |
|---|---|
| Failure troubleshooting | Record-only spans can be promoted when they fail |
| 4xx API outcomes | Normalized as success before promotion |
| Trace continuity | Sampled parents keep all children |
| Parent consistency | Unsampled local parents do not get contradicted by sampled children |
| Noise | Pre-filtering remains explicit and reviewable |
| Upgrade surface | Only the current extension interfaces are documented |

## Effort comparison

| Option | Operational effort | Compatibility | Troubleshooting fit |
|---|---|---|---|
| Keep Ark sampler | Moderate | High | High |
| Use stock parent-based sampling only | Low | High | Low |
| Replace with an external collector | High | Requires deployment changes | High |

## Rollout

Upgrade one service, validate sampled and failed traces plus Rebus metrics, then roll out
incrementally. The profiler is prerelease and experimental; disable it independently if
startup or platform validation finds an incompatibility.
