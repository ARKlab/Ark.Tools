# Application Insights migration implementation plan

Updated: 2026-08-15

This plan is limited to the current Ark.Tools extension interfaces. It contains no schedule
or financial estimates.

## Work completed

- [x] Move package baseline to Application Insights 3.1.2 and OpenTelemetry 1.17.0.
- [x] Keep configuration in `Directory.Packages.props`.
- [x] Register the adaptive sampler through `AddArkApplicationInsightsCustomizations`.
- [x] Preserve failure promotion and 4xx-as-success behavior.
- [x] Enable the prerelease Azure Monitor .NET Profiler in setup extensions.
- [x] Document the upgrade procedure in `docs/otel/upgrade-guide.md`.

## Design acceptance checks

- A sampled local or remote parent samples every child.
- An unsampled local parent records children without exporting them unless failure promotion
  applies.
- A root without a parent uses the adaptive operation budget.
- A failure promotes the failing span, local ancestors, and later sibling spans.
- HTTP 4xx responses are successful outcomes.
- Rebus processing metrics report queue and processing time with stable dimensions.

## Remaining validation

1. Restore and regenerate lock files.
2. Build the solution.
3. Run OTel and Rebus tests, including transport-backed tests where available.
4. Validate profiler startup on each supported hosting model.
5. Compare telemetry continuity and volume during staged rollout.
