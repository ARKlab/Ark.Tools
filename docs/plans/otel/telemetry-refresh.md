# OTel and telemetry refresh scratch todo

Updated: 2026-08-15

## Review and design

- [x] Re-evaluate the branch against the current `master` interface and current package line.
- [x] Reframe `ArkAdaptiveSampler` as an enhancement of parent-based sampling.
- [x] Keep local sampled chains intact.
- [x] Keep unsampled local children `RecordOnly` unless failure promotion applies.
- [x] Preserve failure-first troubleshooting behavior.
- [x] Treat HTTP 4xx as successful outcomes.
- [x] Document the final processor ordering for Web API and Rebus consumers.
- [x] Record failure-registry lifecycle as an operational follow-up.

## Packages and setup

- [x] Refresh Application Insights packages to 3.1.2.
- [x] Refresh OpenTelemetry packages to 1.17.0.
- [x] Enable `Azure.Monitor.OpenTelemetry.Profiler` 1.0.1-beta.7 by default in setup extensions.
- [x] Document profiler validation and platform/deployment follow-up.
- [x] Regenerate lock files and review the package-version changes.

## Rebus metrics

- [x] Add automated coverage for successful queue-time and processing-time metrics.
- [x] Add automated coverage for failed processing metrics.
- [x] Verify `Headers.SentTime` parsing and elapsed-time calculation.
- [x] Verify metric dimensions and names exposed by the processing step.
- [ ] Run transport integration tests with Docker dependencies in an environment with Docker.

## Documentation

- [x] Refresh sampling overview and remove stale algorithm claims.
- [x] Add current-interface, step-by-step upgrade guide.
- [x] Replace stale migration estimates with qualitative effort comparison only.
- [x] Cross-check migration documents for current dates, package versions, and status.
- [x] Link the migration index to the upgrade guide and this scratch todo.

## Validation and handoff

- [x] Run targeted restore/build work and regenerate lock files.
- [x] Run targeted `dotnet build`.
- [x] Run targeted `dotnet test`.
- [ ] Run secret scanning on modified files.
- [ ] Run code review and CodeQL checks.
- [x] Update this file with command results and unresolved follow-up items.

## Validation notes

- OTel tests: 54 passed on .NET 8 and .NET 10.
- Rebus metrics tests: 4 passed on .NET 8 and .NET 10.
- Targeted Application Insights and OTel builds passed with `NuGetAudit=false`;
  SourceLink warnings are expected in the sandbox.
- Full solution restore remains blocked by pre-existing audit findings in unrelated
  dependencies. Resolve those separately before claiming a clean locked restore.
- Secret scanning, automated review, and CodeQL remain handoff tasks.
