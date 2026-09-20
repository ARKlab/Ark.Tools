# Ark.Tools v7.0 beta release notes

Ark.Tools v7 is the first beta since v6.0.6. It introduces an opinionated
solution SDK, privacy-by-default compliance tooling, the Mediator Framework,
and OpenTelemetry-first telemetry. It also removes reflection from several hot
paths.

## Highlights

### Faster hot paths

The following measurements are retained benchmark results from the release
work. Results are workload- and environment-specific; use them as a comparison
of the before and after implementations, not as an application-wide guarantee.

| Area | Improvement |
| --- | --- |
| `ToDataTableArk()` fallback | Compiled accessors are **15.5%–24.7% faster** and allocate **1.1%–3.8% less** for 1–10,000 objects. |
| `ToDataTableArk()` interceptor | Source-generated access is **25.5%–54.7% faster** than the historical reflective path and saves **2,800–242,608 B**. |
| Solid SimpleInjector processors | Cached dispatch changes query/request/command execution from **383.19/401.81/437.20 ns** to **85.04/84.74/75.11 ns**. Allocations fall from **280/280/160 B** to **48 B**. |
| ASP.NET Core model-state filter | Cached metadata changes marked actions from **717.83 ns / 176 B** to **14.13 ns / 0 B**, and unmarked actions from **292.99 ns / 72 B** to **19.30 ns / 0 B**. |
| Problem-details mapping | Cached business-rule accessors reduce empty/single/several-property mappings from **372.6/387.3/512.7 ns** to **222.7/319.4/354.3 ns**, with lower allocation in every case. |
| Mediator Minimal API binding | Cached `TypeConverter` metadata reduces repeated conversion from **59.73 ns** to **15.88 ns** (**73.4%**); both paths allocate **48 B**. |
| Permission authorization | Cached closed requirement types reduce construction from **3.380 µs / 6.49 KB** to **2.066 µs / 5.87 KB**. |

The Solid processor API now also supports self-generic interfaces for a
reflection-free, trim-safe dispatch path. Benchmark figures above apply to the
cached SimpleInjector path; no separate retained A/B figure exists for the
self-generic path.

Benchmark evidence:

- [`ToDataTableArk()` fallback](../benchmarks/Ark.Tools.Core.Benchmarks/results/optimized.md)
- [`ToDataTableArk()` interceptor](../benchmarks/Ark.Tools.Core.Benchmarks/results/interceptor-comparison.md)
- [`Solid dispatch`](design/performance/completed/001-solid-simpleinjector-dispatch.md)
- [`ASP.NET Core model-state metadata`](design/performance/completed/004-model-state-filter-metadata.md)
- [`Problem-details mapping`](design/performance/completed/003-business-rule-problem-details.md)
- [`Minimal API conversion`](design/performance/completed/006-cache-minimal-api-type-converters.md)
- [`Permission requirement types`](design/performance/completed/007-cache-permission-requirement-types.md)

### Ark.Tools.Sdk

`Ark.Tools.Sdk` packages the standard Ark build profile: analyzer configuration,
quality gates, restore and audit policy, packaging defaults, content metadata,
and Microsoft Testing Platform support. It removes the need to copy the same
configuration and analyzer package references into every solution.

Pin the SDK in `global.json`, add it beside `Microsoft.NET.Sdk`, and retain only
application-specific settings in `Directory.Build.props` and
`Directory.Build.targets`. See the [SDK capability reference](sdk/reference.md)
and [v7 migration guide](migration-v7.md#1-adopt-arktoolssdk).

### Ark.Tools.Compliance

`Ark.Tools.Compliance` adds data-classification attributes, sensitive value
objects, `ARKPII*` analyzers, SQL sensitivity and masking policy generation,
and runtime redaction. Compliance analyzer enforcement is opt-in during beta:
set `EnableArkToolsCompliance=true` and add the runtime package where classified
data is declared.

Start with the [Compliance consumer guide](compliance/README.md).

### Ark.Tools.MediatorFramework

The preview Mediator Framework provides source-generated, transport-neutral
contracts and hosting for Minimal APIs, Rebus, gRPC, and Azure Functions.
Migrate endpoint by endpoint: keep handlers and contracts transport-neutral,
generate a replacement route, and remove the controller only after equivalent
integration coverage passes. See [migration from MVC](mediator-framework/migration-from-mvc.md).

### OpenTelemetry is the telemetry contract

Ark instrumentation now targets OpenTelemetry. For new ASP.NET Core hosts,
reference `Ark.Tools.AspNetCore.OTel` and register
`AddArkAzureMonitorOpenTelemetry`. Azure Monitor remains supported through its
OpenTelemetry exporter.

Application Insights v3 is an explicit compatibility route for applications
that still call `TelemetryClient.Track*` or use Application Insights
processors. Application Insights is no longer registered implicitly by Ark
hosting packages. See the [telemetry upgrade guide](otel/upgrade-guide.md).

## Upgrade

Read [Migration to v7](migration-v7.md) before updating production solutions,
especially if they were cloned or ejected from `Ark.ReferenceProject`.
