# Telemetry migration guide

Updated: 2026-08-16

Ark.Tools now treats OpenTelemetry as the instrumentation contract. Microsoft
recommends the Azure Monitor OpenTelemetry Distro for new .NET applications. Choose
the route that matches the application.

## Choose a route

| Application state | Route |
|---|---|
| No custom `ITelemetryClient` or `TelemetryClient.Track*` calls | Migrate to OTel and the Azure Monitor Distro |
| Custom `Track*` calls or direct AI telemetry processors | Keep AI v3 explicitly, then migrate custom calls separately |
| Only Ark Rebus or ResourceWatcher telemetry | Remove AI integration and enable Ark OTel extensions |

The existing instrumentation does not need to be retained solely because an
application exports to Application Insights. AI v3 consumes OpenTelemetry data through
the Azure Monitor exporter. Keep the compatibility adapters only for code that directly
uses the AI object model.

## New application: Azure Monitor OTel Distro

1. Reference `Ark.Tools.AspNetCore.OTel` for an ASP.NET Core host.
2. Call `builder.Services.AddArkAzureMonitorOpenTelemetry()` before `Build()`.
3. Set `APPLICATIONINSIGHTS_CONNECTION_STRING` in the deployment environment.
4. Register Rebus instrumentation explicitly:

   ```csharp
   options.UseOpenTelemetry(container);
   options.UseOpenTelemetryMetrics(container);
   ```

5. Reference `Ark.Tools.ResourceWatcher.OTel` for a ResourceWatcher worker and call
   `builder.AddArkOpenTelemetryForWorkerHost()`.
6. Add the exporter required by the host. The Ark instrumentation packages do not
   select an exporter or read a connection string.

The sample web host follows this route in
`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface`.

## Existing application: Application Insights v3

1. Keep explicit references to `Microsoft.ApplicationInsights.AspNetCore` or
   `Microsoft.ApplicationInsights.WorkerService` version 3.x.
2. Keep the matching Ark host package:
   `Ark.Tools.AspNetCore.ApplicationInsights` or
   `Ark.Tools.ApplicationInsights.HostedService`.
3. For ResourceWatcher, add
   `Ark.Tools.ResourceWatcher.ApplicationInsights`.
4. For legacy Rebus request and metric items, add
   `Ark.Tools.Rebus.ApplicationInsights`.
5. Register the Microsoft SDK first, then the Ark compatibility extension.
6. Keep custom `TelemetryClient.Track*` calls until their replacement telemetry has
   been reviewed.

Application Insights is no longer registered by `Ark.Tools.AspNetCore`,
`Ark.Tools.AspNetCore.MinimalApi`, or `Ark.Tools.ResourceWatcher.WorkerHost.Hosting`.
Existing applications must add the compatibility registration intentionally.

## Breaking telemetry changes

The following mappings are the complete Ark Rebus and ResourceWatcher migration surface.
Operation names, payload key names, message type values, result values, and queue-time
success-only behavior are retained where possible.

| Area | Before | After | Monitoring adaptation |
|---|---|---|---|
| Rebus processing | AI `RequestTelemetry` | OTel consumer span named `Rebus.Process \| <MessageType>` | Query spans and `messaging.*`/`rebus.*` attributes instead of request items |
| Rebus queue time | AI metric `Rebus / MessageTimeInQueueSuccess` | OTel histogram `Rebus.MessageTimeInQueueSuccess` | Change metric provider syntax; keep `MessageType` |
| Rebus processing time | AI metric `Rebus / MessageProcessingTime` | OTel histogram `Rebus.MessageProcessingTime` | Change metric provider syntax; keep `MessageType` and `OperationResult` |
| Rebus success/failure | `OperationResult=success/failure` | Same attribute values | No value change |
| Rebus trace propagation | `Diagnostic-Id` header | Same header, parsed as W3C trace context | No rule change; verify W3C parent correlation |
| ResourceWatcher run/process spans | AI request telemetry | OTel internal spans using existing operation names | Remove telemetry-type filters; retain operation-name and attribute filters |
| ResourceWatcher fetch/state spans | AI dependency telemetry with `Type=ProcessStep` | OTel internal spans using existing operation names | Replace dependency-type filters with span-name filters |
| ResourceWatcher exceptions | AI `ExceptionTelemetry` | OTel exception event plus error status on the operation span | Query exception events/status instead of exception item type |
| ResourceWatcher event warnings | AI event telemetry | OTel span/event attributes | Update event-type filters; retain tenant/resource attributes |
| HTTP 4xx | AI processor clears failure | OTel processor clears server span error status | Keep 4xx-as-success alert rules at the span-status level |
| Registration | Hosting package implicitly added AI | No automatic registration | Add one explicit OTel or AI setup call |

The main unavoidable dashboard change is the telemetry item type. The operation
names, dimensions, values, and failure semantics are intentionally stable.

## `ITelemetryClient` decision

`ITelemetryClient` and `TelemetryClient.Track*` are Application Insights SDK APIs, not
OpenTelemetry APIs. Ark.Tools will not emulate them in OTel instrumentation.

Applications using them should remain on the explicit AI v3 compatibility route until
each custom telemetry call is ported to an `Activity`, `Meter`, or OTel log/event.
Applications without those calls should use the OTel route immediately; Ark Rebus and
ResourceWatcher instrumentation no longer require the AI SDK.

## Rollout checklist

- Compare span counts and parent/child continuity.
- Compare Rebus queue and processing histograms by message type.
- Compare ResourceWatcher operation names, tenant/resource attributes, and failures.
- Update alert queries for OTel span and metric types.
- Roll out one instance before removing AI compatibility packages.
