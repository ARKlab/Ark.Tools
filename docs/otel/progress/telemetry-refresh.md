# OTel and telemetry refresh scratch todo

Updated: 2026-08-15

## Review and design

- [x] Re-evaluate the branch against the current `master` interface and current package line.
- [x] Reframe `ArkAdaptiveSampler` as an enhancement of parent-based sampling.
- [x] Keep local sampled chains intact.
- [x] Keep unsampled local children `RecordOnly` unless failure promotion applies.
- [x] Preserve failure-first troubleshooting behavior.
- [x] Treat HTTP 4xx as successful outcomes.
- [ ] Confirm the final processor ordering in an application using both Web API and Rebus.
- [ ] Decide whether failure registry cleanup should be tied to provider shutdown.

## Packages and setup

- [x] Refresh Application Insights packages to 3.1.2.
- [x] Refresh OpenTelemetry packages to 1.17.0.
- [x] Enable `Azure.Monitor.OpenTelemetry.Profiler` 1.0.1-beta.7 by default in setup extensions.
- [ ] Confirm profiler behavior on every supported hosting model and document any platform exclusions.
- [ ] Regenerate and inspect all lock files after restore.

## Rebus metrics

- [ ] Add an automated test for successful queue-time and processing-time metrics.
- [ ] Add an automated test for failed processing metrics.
- [ ] Verify `Headers.SentTime` parsing and clock skew behavior.
- [ ] Verify metric dimensions and names in Application Insights 3.x.
- [ ] Run transport integration tests with Docker dependencies.

## Documentation

- [x] Refresh sampling overview and remove stale algorithm claims.
- [x] Add current-interface, step-by-step upgrade guide.
- [ ] Replace stale migration estimates with effort comparison only.
- [ ] Cross-check every migration document for dates, package versions, and completed status.
- [ ] Add links from the migration index to the upgrade guide and this scratch todo.

## Validation and handoff

- [ ] Run `dotnet restore` and regenerate lock files.
- [ ] Run `dotnet build`.
- [ ] Run `dotnet test`.
- [ ] Run secret scanning on modified files.
- [ ] Run code review and CodeQL checks.
- [ ] Update this file with final command results and unresolved follow-up items.
