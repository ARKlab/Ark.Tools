# Adaptive sampling in Ark.Tools

Updated: 2026-10-15

## Decision

`ArkAdaptiveSampler` is a parent-sampling enhancement. It applies an adaptive budget to
roots, then preserves the parent decision for descendants. This is intentionally stricter
than sampling every child independently: a trace is either kept as a chain or retained as
record-only data that can still be promoted when troubleshooting needs it.

## Decision table

| Situation | Decision |
|---|---|
| Pre-filtered successful noise | Drop |
| Root with no parent and available budget | Record and sample |
| Root with no parent and exhausted budget | Record only |
| Local sampled parent | Record and sample |
| Remote sampled parent, `RespectRemoteParentSampling` `false` (default) | Budget decision, as a root |
| Remote sampled parent, `RespectRemoteParentSampling` `true` | Record and sample |
| Local unsampled parent | Record only |
| Remote unsampled parent, `RespectRemoteParentSampling` `false` (default) | Budget decision, as a root |
| Remote unsampled parent, `RespectRemoteParentSampling` `true` | Record only |
| Span that ends with `Error` status | Promote to sampled |
| HTTP 4xx response without error status | Success; do not promote |
| HTTP 4xx response with error status | Promote to sampled |

`RecordOnly` is deliberate. It preserves the activity long enough for the completion
processor to inspect its status.

Only the roots that the bucket logic decides count in the budget: dropped and filtered spans do
not. The sampler applies the pre-filter rules (HTTP `OPTIONS`, SQL commits) and every
`ArkAdaptiveSamplerOptions.Filter` (the NLog database filter, and the Service Bus receive rule of
`Ark.Tools.OTel.Azure`) to the span name and creation tags, and drops matching spans before they
reach a bucket. `Filter` returns `false` to drop a span; the Ark setups compose their rules onto it
from `PostConfigure`, so an application filter set with `configureAdaptiveSampler` or
`services.Configure<ArkAdaptiveSamplerOptions>` is kept. Spans that an Ark filter recognizes only after sampling, such as SQL commands
skipped by query label (`outbox.peek-lock` and `SqlQueryLabelsToSkip`) or by an application
SqlClient filter, and spans the pre-filter or NLog processors drop when they start or end, give
back the token and the traffic they counted. Spans dropped after sampling by other processors
or instrumentation filters, such as an application `HttpClient` filter, still count. HTTP `OPTIONS` requests are recognized before sampling when ASP.NET Core passes the request
method at creation: by default from ASP.NET Core 11, and with `AddArkAspNetCoreOpenTelemetry`
on .NET 10, which sets the
`Microsoft.AspNetCore.Hosting.SuppressActivityOpenTelemetryData` switch to `false` unless the
application already set it.

## Failure-first troubleshooting

At completion, `ArkFailurePromotionProcessor`:

1. Treats a span as a failure only when its status is `Error`.
2. Registers the trace as failed.
3. Promotes the failing span and all live local ancestors.
4. Promotes siblings that finish after the failure is observed.
5. Causes new descendants to be sampled through the shared failure registry.

Siblings that ended before the failure was observed cannot be recovered. This is the
intentional boundary that avoids retaining every operation indefinitely.

The code that produces a span decides whether it failed, through its status. An `Unset` or `Ok`
span is not a failure, even with an `exception` event, an HTTP 5xx code or a gRPC error code: Ark
does not second-guess the producer. The instrumentations Ark wires set `Error` by convention:

| Instrumentation | Sets `Error` on |
|---|---|
| ASP.NET Core server | 5xx responses and exceptions that escape the pipeline |
| `HttpClient` (.NET 9 and later: the runtime's own spans) | 4xx and 5xx responses and exceptions |
| SQL Client | failed commands |
| Azure SDK (`Azure.*` sources) | failed operations, with `error.type` |
| Rebus `OpenTelemetryStep`, MediatorFramework messaging steps, Outbox, ResourceWatcher | exceptions |
| MediatorFramework gRPC server (`ArkGrpcErrorInterceptor`) | unexpected exceptions, which it maps to `Internal` |

gRPC has no other status mapping by default. A gRPC call ends with HTTP 200, so the ASP.NET Core
instrumentation leaves the request span `Unset` unless its experimental
`OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_ENABLE_GRPC_INSTRUMENTATION` support is on, and
`Grpc.Net.Client` never sets the status of its client spans. Add
`OpenTelemetry.Instrumentation.GrpcNetClient` to mark failed gRPC client calls, or set `Error`
where you handle them. Code of your own that records an exception on a span must also set `Error`
if the span failed.

HTTP 4xx codes are not failures by themselves: a 4xx span is promoted only when its status is
`Error`. Ark does not change the status of server 4xx spans, except that the Ark Azure setup sets
`Ok` on `Unset` ones, see [HTTP 4xx status](#http-4xx-status).
An application processor registered before the Ark setup can clear the status of expected client
errors, such as Azure Storage 404 or 409.

## HTTP 4xx status

The code that produces a span decides whether it failed. Some 4xx responses are errors, most are
not, and Ark does not erase that decision: a server 4xx is not an Ark failure, and Ark leaves its
status as the instrumentation or the application set it.

- **OpenTelemetry convention.** A server 4xx leaves the span status `Unset`, a server 5xx is
  `Error`, and a client 4xx or 5xx is `Error`
  ([HTTP spans, Status](https://github.com/open-telemetry/semantic-conventions/blob/v1.44.0/docs/http/http-spans.md?plain=1#L82-L113)).
- **ASP.NET Core instrumentation** (`OpenTelemetry.Instrumentation.AspNetCore` 1.19.0) follows it:
  the status comes from the response code only while it is still `Unset`, with a lower bound of
  500 for server spans
  ([SpanHelper](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/Instrumentation.AspNetCore-1.19.0/src/Shared/SpanHelper.cs#L20-L25),
  [HttpInListener](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/Instrumentation.AspNetCore-1.19.0/src/OpenTelemetry.Instrumentation.AspNetCore/Implementation/HttpInListener.cs#L385-L388)).
  It sets `Error` and `error.type` whatever the final response code when it receives
  `Microsoft.AspNetCore.Hosting.UnhandledException` or `Microsoft.AspNetCore.Diagnostics.UnhandledException`
  ([HttpInListener.OnException](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/Instrumentation.AspNetCore-1.19.0/src/OpenTelemetry.Instrumentation.AspNetCore/Implementation/HttpInListener.cs#L433-L440)).
- **ASP.NET Core 10** writes `Hosting.UnhandledException` only when an exception escapes the
  pipeline
  ([HostingApplicationDiagnostics](https://github.com/dotnet/aspnetcore/blob/v10.0.9/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L217-L225)),
  and `Diagnostics.UnhandledException` only from the developer exception page
  ([DeveloperExceptionPageMiddlewareImpl](https://github.com/dotnet/aspnetcore/blob/v10.0.9/src/Middleware/Diagnostics/src/DeveloperExceptionPage/DeveloperExceptionPageMiddlewareImpl.cs#L165-L169)).
  `UseExceptionHandler` writes `Microsoft.AspNetCore.Diagnostics.HandledException` instead, which
  the instrumentation does not handle, unless diagnostics are suppressed: by default when an
  `IExceptionHandler` handled the exception, and as the application decides with
  `ExceptionHandlerOptions.SuppressDiagnosticsCallback`
  ([ExceptionHandlerMiddlewareImpl](https://github.com/dotnet/aspnetcore/blob/v10.0.9/src/Middleware/Diagnostics/src/ExceptionHandler/ExceptionHandlerMiddlewareImpl.cs#L218-L255)).
  MVC exception filters raise neither event. So an exception that the application maps to a 4xx
  response, for example with `ArkProblemDetailsExceptionHandler`, leaves the span `Unset`.
- **Application Insights** (`Azure.Monitor.OpenTelemetry.Exporter` 1.10.0) marks a request as
  successful when its status is `Ok`, failed when it is `Error`, and, when it is `Unset`, successful
  only for response codes below 400
  ([RequestData](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Monitor.OpenTelemetry.Exporter_1.10.0/sdk/monitor/Azure.Monitor.OpenTelemetry.Exporter/src/Customizations/Models/RequestData.cs#L87-L100)).
  The `requests/failed` standard metric uses the same rule
  ([StandardMetricsExtractionProcessor](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Monitor.OpenTelemetry.Exporter_1.10.0/sdk/monitor/Azure.Monitor.OpenTelemetry.Exporter/src/Internals/StandardMetricsExtractionProcessor.cs#L211)),
  and a dependency is successful unless its status is `Error`
  ([RemoteDependencyData](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Monitor.OpenTelemetry.Exporter_1.10.0/sdk/monitor/Azure.Monitor.OpenTelemetry.Exporter/src/Customizations/Models/RemoteDependencyData.cs#L78)).
  So, left alone, Application Insights shows a server 4xx that follows the OpenTelemetry convention
  as a failed request. This is the exporter's rule, not an Ark failure: Ark does not promote the span.
- **Live metrics** (same exporter) count a request as failed only when its status is `Error`: the
  response code is read but never used, so `Unset` and `Ok` 4xx requests are both successes there
  ([DocumentHelper](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Monitor.OpenTelemetry.Exporter_1.10.0/sdk/monitor/Azure.Monitor.OpenTelemetry.Exporter/src/LiveMetrics/Internals/DataCollection/DocumentHelper.cs#L181-L246)).

`AddArkAzureOpenTelemetry` sets `Ok` on server spans that end with a 4xx code and an `Unset`
status, so Application Insights shows them as successful requests, as with Ark v6 and the
Application Insights SDK. A 4xx that the application or an unhandled exception marked as `Error`
stays failed, and an `Ok` set by the application is kept. The processor runs after the Ark
processors, failure promotion included, and after the application processors registered before
the Ark setup; the exporter adds its own processors (live metrics, standard metrics, export) when
the host starts, after it
([ExporterRegistrationHostedService](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Monitor.OpenTelemetry.Exporter_1.10.0/sdk/monitor/Azure.Monitor.OpenTelemetry.Exporter/src/ExporterRegistrationHostedService.cs#L65-L77)).
It is registered only when the connection string is configured. Failure promotion does not change:
`Unset` and `Ok` spans are not failures. To keep the exporter's default and show `Unset` 4xx
requests as failed, pass `clientErrorsAsSuccess: false`:

```csharp
services.AddOpenTelemetry()
    .AddArkAspNetCoreOpenTelemetry(configuration: configuration)
    .AddArkAzureOpenTelemetry(configuration, clientErrorsAsSuccess: false);
```

A setup that exports to Azure Monitor without `AddArkAzureOpenTelemetry` sets `Ok` itself. Leave an
`Error` in place, so 4xx responses that the application or an unhandled exception marked as failed
stay failed:

```csharp
public sealed class ClientErrorsAsSuccessProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity data)
    {
        if (data.Kind == ActivityKind.Server
            && data.Status == ActivityStatusCode.Unset
            && data.GetTagItem("http.response.status_code") is int code and >= 400 and < 500)
            data.SetStatus(ActivityStatusCode.Ok);
    }
}

// Before the Ark setup, so the processor runs before the exporter.
services.ConfigureOpenTelemetryTracerProvider(tracing =>
    tracing.AddProcessor(new ClientErrorsAsSuccessProcessor()));
```

An application that wants some 4xx responses to be errors, such as a 409 that should never
happen, sets `Error` on the request span, for example with
`Activity.Current?.SetStatus(ActivityStatusCode.Error, "...")` in the endpoint: the instrumentation
keeps it, and Ark promotes the span.

`WebApi4xxAsSuccessProcessor` is no longer registered by the Ark setups and is obsolete. It set
every server 4xx span to `Unset`, which erased an `Error` set on purpose and did not change how
Application Insights counts the request. An application that still registers it before the Ark setup
with `AddArkAzureOpenTelemetry` gets `Unset` from it and then `Ok`, so every server 4xx, `Error` included,
is shown as a success.

## Root rate control

Root spans use a token bucket keyed by operation name. The default burst capacity is two
seconds of tokens, and at least one token. `TracesPerSecond` is the total target for all
buckets. Every `SamplingPercentageDecreaseTimeout`, the moving-average controller measures
the traffic of each bucket and shares the budget max-min fairly: operations slower than the
fair share get a refill rate that covers their average traffic, and the busier operations split
the rest evenly. A burst larger than the bucket is still limited, so a rare operation that sends
many roots at once keeps only the first ones. Idle
operations, and new ones, split the budget the active operations leave unused, so the buckets
stay within the budget when idle operations resume; each still holds one token, so its first
root is sampled. An idle bucket also refills up to one token per `SamplingPercentageDecreaseTimeout`,
capped at an even split of `TracesPerSecond` among the idle buckets, so an operation that skips whole
periods, such as a periodic job or a health check, is sampled every time it runs while its traffic
fits its share, also next to a busy operation that uses the rest of the budget. This refill can
take the idle buckets above the budget the active ones leave, by at most `TracesPerSecond` in
total. Before the first adjustment every bucket starts at `TracesPerSecond`, and many
operations first seen between two adjustments can also exceed the target until the next one.
`MovingAverageRatio` smooths rate increases only: a bucket above its new
target drops to it at once, so after an adjustment the active buckets never share more than the
budget. `MaxOperationBuckets` limits memory when operation names are dynamic;
overflow shares one bucket.

## Sample rate and item counts

Application Insights estimates volumes from the `sampleRate` of each item:
`itemCount = 100 / sampleRate`. Like the Azure Monitor samplers, `ArkAdaptiveSampler`
puts the `microsoft.sample_rate` attribute (0–100) on the spans it samples:

| Span | `microsoft.sample_rate` |
|---|---|
| Root sampled by its bucket | `100 / n`, where `n` counts the root and the roots the bucket skipped since its previous sampled root |
| Local descendant of a sampled span | The parent's rate, copied by `ArkFailurePromotionProcessor` when the span starts |
| Span kept because its trace failed, including the failing span, its ancestors and later spans of the trace | `100` (itemCount 1) |
| Span with a remote parent | As a root; with `RespectRemoteParentSampling`, none (itemCount 1): the upstream rate is not propagated |

Without failures, the sum of the item counts of the exported roots matches the number of roots
seen. The roots a bucket skipped are counted when it samples its next root, so short time
windows lag slightly, and the roots skipped after an operation's last sampled root are never
counted. Failures are kept whatever the budget, so they count only themselves. A promoted root
is also counted in its bucket's next sampled root, and a sampled root that fails no longer
stands for the roots skipped before it; sampling does not depend on the outcome, so these
effects cancel out on average and the sum stays an unbiased estimate. The rate is copied from `Activity.Parent`, so a span started with an
explicit parent context carries no rate.

`Azure.Monitor.OpenTelemetry.Exporter` 1.10.0 does not read the attribute yet: it sets the
`sampleRate` of every item from `AzureMonitorExporterOptions.SamplingRatio`, so Application
Insights still reports `itemCount = 1` for every sampled span, as it does with the exporter's own
rate-limited sampler. Exporters that read the attribute weight the spans correctly. Until the
.NET exporter does, use the unsampled OpenTelemetry metrics, such as
`http.server.request.duration`, for volumes, rates and count-based alerts.

## Configuration

```json
{
  "OTel": {
    "ArkAdaptiveSampler": {
      "TracesPerSecond": 1.0,
      "MovingAverageRatio": 0.5,
      "SamplingPercentageDecreaseTimeout": "00:01:00",
      "EnablePerOperationBucketing": true,
      "MaxOperationBuckets": 100,
      "RespectRemoteParentSampling": false
    }
  }
}
```

By default (`RespectRemoteParentSampling` is `false`) a span with a remote parent is a root for
the local budget and carries this service's sample rate, as with the Azure Monitor samplers, so
callers that use a different sampler, such as the classic Application Insights SDK, do not
decide for the service. Traces that cross services which decide differently can then be
incomplete; failure promotion applies either way. Set it to `true` to follow the W3C sampled
flag of the remote parent instead: such spans then carry no sample rate (itemCount 1).

The option applies to every remote parent of the host, not to one transport:

| Incoming span | Parent read from |
|---|---|
| ASP.NET Core request | `traceparent` (and `tracestate`) HTTP headers |
| Rebus message (`OpenTelemetryStep`, `ark.tools.rebus.process`) | `Diagnostic-Id` and `TraceStateString` message headers |
| MediatorFramework message (`OpenTelemetryIncomingStep`, `amf.message.process`) | `Diagnostic-Id` message header |

So with the default a Rebus or MediatorFramework worker samples its messages with its own
budget, and some of its traces start in the middle of a chain whose sender was not sampled. A
worker that should keep only the messages whose sender was sampled, and keep all of them, sets
`OTel:ArkAdaptiveSampler:RespectRemoteParentSampling` to `true`; that also applies to the HTTP
requests the same host receives.

The legacy `ApplicationInsightsStep` of `Ark.Tools.Rebus.ApplicationInsights` starts its span
without an `ActivitySource`, so no sampler sees it: it always follows the sender's sampled flag.

`OTel:ArkAdaptiveSampler` is the canonical section for the OpenTelemetry setup
(`AddArkOpenTelemetry` or `AddArkAspNetCoreOpenTelemetry` when given an `IConfiguration`,
and `AddArkOpenTelemetryForWorkerHost`). `AddArkAzureOpenTelemetry(configuration)` also reads
the legacy `ApplicationInsights:ArkAdaptiveSampler` section for compatibility; values
in `OTel:ArkAdaptiveSampler` take precedence. The Application Insights v3 compatibility path
(`AddArkApplicationInsightsCustomizations`) reads only
`ApplicationInsights:ArkAdaptiveSampler`.

## Operational checks

- Verify the root decision before investigating child sampling.
- Verify 4xx normalization before treating missing traces as failures.
- Verify sampled parent continuity across process boundaries.
- Expect only spans that finish after a failure to be retroactively promoted.
- Watch operation-name cardinality and the configured bucket cap.
