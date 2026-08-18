# Ark.Tools telemetry implementation plan

Updated: 2026-08-16

This plan implements the decisions in [design.md](design.md). It deliberately
separates the compatibility path from the OTel-first path so package dependency
graphs make the choice visible.

## Phase 1: package and dependency boundaries

1. Add dedicated `Ark.Tools.AspNetCore.OTel`, `Ark.Tools.ResourceWatcher.OTel`, and
   `Ark.Tools.Rebus.ApplicationInsights` projects.
2. Move all Rebus SDK usage out of `Ark.Tools.Rebus`; make its instrumentation use
   `ActivitySource`, `Meter`, and OTel-compatible context propagation.
3. Keep Application Insights adapters in dedicated packages only.
4. Remove Application Insights project references from general hosting projects.
5. Add the new projects to `Ark.Tools.slnx`, central package management, and lockfile
   generation.

## Phase 2: explicit setup extensions

1. Add `AddArkAzureMonitorOpenTelemetry` for ASP.NET Core and register the Ark
   ActivitySource/Meter names.
2. Add `AddArkOpenTelemetryForWorkerHost` for ResourceWatcher worker hosts.
3. Add `UseOpenTelemetry` and `UseOpenTelemetryMetrics` Rebus pipeline extensions.
4. Preserve the legacy Rebus extension names in the compatibility package and
   document that the package is opt-in.
5. Remove automatic Application Insights setup from `ArkStartupBase` and the
   ResourceWatcher worker hosting package.

## Phase 3: ResourceWatcher adapter

1. Expose the existing ResourceWatcher activity and diagnostic source names as the
   OTel instrumentation contract.
2. Implement the OTel listener/registration without changing resource-provider
   callbacks or the state machine.
3. Keep the existing AI listener unchanged in its dedicated package.
4. Verify operation names, payload attributes, exception status, and duration units.

## Phase 4: samples and migration documentation

1. Convert the mediator web sample to the Azure Monitor OTel Distro setup extension.
2. Keep a focused AI v3 sample/compatibility test showing explicit registration for
   existing applications.
3. Rewrite the upgrade guide around two routes:
   - OTel Distro for applications without custom `ITelemetryClient.Track*` calls.
   - AI v3 compatibility for applications that still use those calls.
4. List every Rebus and ResourceWatcher telemetry mapping that can affect alerts.

## Phase 5: verification and rollout

1. Build the changed source projects and samples.
2. Run Rebus, OTel, and ResourceWatcher tests.
3. Inspect dependency graphs to verify core hosting packages do not transitively
   install Application Insights.
4. Regenerate and review lockfiles.
5. Run secret scanning, automated review, and CodeQL.
6. Roll out the OTel path beside the compatibility path and compare span names,
   metric dimensions, failure counts, and alert results before removing old rules.

## Acceptance criteria

- Installing `Ark.Tools.AspNetCore`, `Ark.Tools.AspNetCore.MinimalApi`, or
  `Ark.Tools.ResourceWatcher.WorkerHost.Hosting` does not install Application Insights.
- Rebus and ResourceWatcher instrumentation compile without Application Insights
  references.
- New samples explicitly register the Azure Monitor OTel Distro.
- Existing AI v3 applications have a documented, explicit compatibility path.
- Queue-time and processing-time semantics remain unchanged.
- All telemetry breaking changes are listed with migration actions.
