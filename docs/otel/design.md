# Ark.Tools telemetry design

Updated: 2026-08-16

## Decision

Ark.Tools is OpenTelemetry-first. Instrumentation packages produce OpenTelemetry
activities, meters, and events; they do not reference the Application Insights SDK.
Application Insights remains a supported exporter and compatibility path, but it is
selected by an application through a dedicated package and explicit setup call.

The Azure Monitor OpenTelemetry Distro is the recommended new-application path:

```csharp
builder.Services.AddArkAzureMonitorOpenTelemetry(builder.Configuration);
```

The extension is opt-in. It configures the Microsoft distro and registers Ark
instrumentation sources. The application owns the connection string and can select a
different OTel exporter without changing Ark instrumentation.

## Package boundaries

| Package | Responsibility | Application Insights dependency |
|---|---|---|
| `Ark.Tools.OTel` | Ark sampling, filtering, enrichment processors | None |
| `Ark.Tools.Rebus` | Rebus spans and metrics through OTel APIs | None |
| `Ark.Tools.ResourceWatcher` | ResourceWatcher activities and diagnostic events | None |
| `Ark.Tools.AspNetCore` | Hosting defaults and web pipeline | None |
| `Ark.Tools.AspNetCore.OTel` | Azure Monitor OTel Distro setup and Ark sources | None |
| `Ark.Tools.ResourceWatcher.OTel` | ResourceWatcher OTel setup | None |
| `Ark.Tools.ApplicationInsights` | AI v3 compatibility customizations | Explicit |
| `Ark.Tools.AspNetCore.ApplicationInsights` | AI v3 hosting setup and processors | Explicit |
| `Ark.Tools.ApplicationInsights.HostedService` | AI v3 worker setup | Explicit |
| `Ark.Tools.ResourceWatcher.ApplicationInsights` | AI v3 ResourceWatcher adapter | Explicit |
| `Ark.Tools.Rebus.ApplicationInsights` | AI v3 Rebus compatibility adapter | Explicit |

Hosting packages never reference the Application Insights packages. In particular,
`Ark.Tools.AspNetCore`, `Ark.Tools.AspNetCore.MinimalApi`, and
`Ark.Tools.ResourceWatcher.WorkerHost.Hosting` do not register telemetry by default.
An application must call either the OTel setup extension or the AI compatibility
extension.

## Instrumentation contracts

### Rebus

`Ark.Tools.Rebus` creates an `ActivitySource` and `Meter` named
`ark.tools.rebus`. The incoming processing span carries message and correlation
context, and the outgoing step propagates the current trace identifier through the
existing outbox header.

The metrics retain the current logical measurements and dimensions:

| OTel instrument | Attributes |
|---|---|
| `ark.tools.rebus.message_time_in_queue_success` | `message.type` |
| `ark.tools.rebus.message_processing_time` | `message.type`, `operation.result` |

Queue time is emitted only after successful processing. Processing time is emitted for
success and failure. Missing or invalid sent-time headers do not affect message
processing.

### ResourceWatcher

`Ark.Tools.ResourceWatcher` emits lowercase, dot-separated OpenTelemetry scope and
activity names. `Ark.Tools.ResourceWatcher.OTel` enables its source and meter for
an exporter-agnostic OTel pipeline. Existing diagnostic listeners remain available
for non-OTel consumers; the AI v3 worker adapter enables the same ActivitySource
and Meter directly instead of creating duplicate legacy telemetry.

The OTel path maps the existing operation payload to span attributes and exception
events. Process-wide identity is configured on the OTel Resource rather than copied
onto every span by a processor. No application code changes are required to resource
providers or processors.

## Application Insights v3 compatibility

Applications that use `ITelemetryClient` or call `Track*` are not transparent OTel
migrations. They should:

1. Keep the `Microsoft.ApplicationInsights` v3 packages explicitly referenced.
2. Add the corresponding Ark compatibility package.
3. Register the v3 SDK first, then register the Ark compatibility adapter.
4. Migrate custom `Track*` calls independently when adopting OTel.

Applications that only consume Ark Rebus or ResourceWatcher telemetry should remove
the compatibility package and use the OTel setup extension. This preserves the
instrumentation source and avoids an Application Insights SDK dependency.

## Compatibility and breaking changes

The following changes are intentional and must be checked against dashboards and
alerts:

| Existing behavior | OTel behavior | Required action |
|---|---|---|
| AI `RequestTelemetry` for Rebus processing | `ark.tools.rebus` span | Update queries from AI request fields to span attributes |
| AI custom metrics with component/name/dimensions | OTel histogram with the same logical names and attributes | Update metric namespace/query syntax |
| AI ResourceWatcher request/dependency telemetry | OTel internal spans with lowercase dot-separated operation names | Update telemetry-type and operation-name filters |
| AI `ExceptionTelemetry` items | OTel exception events and error status | Update exception queries to span events/status |
| Implicit AI registration from a hosting package | No telemetry registration | Add an explicit OTel or AI setup call |

Message type and result values remain stable. ResourceWatcher operation names and
attributes now follow OTel lowercase/dot-separated and snake_case conventions.
Success-only queue-time semantics are retained.

## Sampling and exporters

`Ark.Tools.OTel` remains exporter-neutral. Its processors and sampler are added to
the application's OTel tracer provider. Azure Monitor sampling and export are owned
by the Azure Monitor Distro setup extension; applications using the legacy AI v3
path use the existing AI bridge package.

## Non-goals

- Keep Application Insights SDK references in core instrumentation packages.
- Emulate every `ITelemetryClient.Track*` API.
- Automatically choose an exporter or connection string.
- Remove the AI compatibility packages in this release.
