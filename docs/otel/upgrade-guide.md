# Ark.Tools OTel interface upgrade guide

This guide covers the current Ark.Tools extension interfaces in `master`. It intentionally
does not describe removed Application Insights v2 APIs or internal implementation details.

## 1. Inventory the current integration

Identify every application that calls one of these extensions:

- `ArkApplicationInsightsTelemetry(IServiceCollection, IConfiguration)`
- `AddApplicationInsightsForHostedService(IHostBuilder)`
- `AddApplicationInsightsCustomizations(IServiceCollection, IConfiguration, string?)`

Keep the extension call after the matching Application Insights registration. The extension
installs the Ark sampler and processors through the Application Insights OpenTelemetry
pipeline.

## 2. Update package versions

Update the centrally managed versions in `Directory.Packages.props`, then restore. The
supported baseline is:

- `Microsoft.ApplicationInsights`, `Microsoft.ApplicationInsights.AspNetCore`, and
  `Microsoft.ApplicationInsights.WorkerService` 3.1.2
- `OpenTelemetry`, `OpenTelemetry.Api`, and `OpenTelemetry.Extensions.Hosting` 1.17.0
- `Azure.Monitor.OpenTelemetry.Profiler` 1.0.1-beta.7

Do not add direct package versions to consuming projects. Regenerate every affected
`packages.lock.json` and verify locked-mode restore.

## 3. Apply the ASP.NET Core setup

1. Keep the existing `services.AddApplicationInsightsTelemetry(...)` call.
2. Keep the existing connection-string or instrumentation-key configuration.
3. Call `services.AddAzureMonitorProfiler()` immediately after Application Insights setup.
4. Keep `services.AddArkApplicationInsightsCustomizations(...)` after both registrations.
5. Set `APPLICATIONINSIGHTS_CONNECTION_STRING` in the deployment environment.
6. Deploy and check startup logs for profiler initialization.

The profiler package is prerelease and the integration is experimental. Treat profiler
startup failure as non-fatal and validate the application without profiler data first.

## 4. Apply the hosted-service setup

1. Keep `services.AddApplicationInsightsTelemetryWorkerService(...)`.
2. Call `services.AddAzureMonitorProfiler()` after that registration.
3. Keep `services.AddArkApplicationInsightsCustomizations(...)` last.
4. Configure the same connection-string environment variable.
5. Confirm the worker starts even when profiler activation is unavailable.

## 5. Review sampling behavior

The adaptive sampler is an enhancement of parent-based sampling:

- A recorded parent records every child.
- A local parent that was not recorded makes children `RecordOnly`; a child is not sampled
  independently.
- A failure promotes the failing span, local parent chain, and spans that finish afterward.
- HTTP 4xx responses are expected successful outcomes and must be normalized by
  `WebApi4xxAsSuccessProcessor` before failure promotion.
- A root without a parent uses the adaptive per-operation budget.

Test one sampled root, one unsampled root, a remote sampled parent, a 4xx response, and a
5xx/exception response before rollout.

## 6. Validate Rebus metrics

Install the Rebus metrics step through `UseApplicationInsightMetrics`. Send a message with
`Headers.SentTime`, then verify:

1. `Rebus / Message TimeInQueue (Success)` is emitted only after successful processing.
2. `Rebus / Message ProcessingTime` is emitted for both success and failure.
3. `MessageType` is stable and non-empty.
4. `OperationResult` is `success` or `failure`.
5. Queue time is total elapsed time minus processing time and is never negative.

Run the Rebus integration tests with the repository's Docker dependencies when validating
transport-specific behavior.

## 7. Rollout and rollback

Deploy to one instance first. Compare request counts, failure counts, sampled trace
continuity, profiler startup, and Rebus metrics. Roll back by removing the profiler package
and `AddAzureMonitorProfiler()` call; the existing Application Insights and Ark OTel
extensions remain independently usable.
