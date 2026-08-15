# NuGet refresh record

Updated: 2026-08-15

Versions were checked against NuGet on the update date:

| Package | Version | Reason |
|---|---:|---|
| Microsoft.ApplicationInsights | 3.1.2 | Current stable 3.x SDK |
| Microsoft.ApplicationInsights.AspNetCore | 3.1.2 | Current ASP.NET Core integration |
| Microsoft.ApplicationInsights.WorkerService | 3.1.2 | Current worker integration |
| Microsoft.ApplicationInsights.NLogTarget | 3.1.2-beta1 | Current NLog target line |
| OpenTelemetry | 1.17.0 | Current stable SDK |
| OpenTelemetry.Api | 1.17.0 | SDK alignment |
| OpenTelemetry.Extensions.Hosting | 1.17.0 | SDK alignment |
| OpenTelemetry.Extensions.Propagators | 1.17.0 | SDK alignment |
| Azure.Monitor.OpenTelemetry.Profiler | 1.0.1-beta.7 | Latest prerelease requested by the setup design |

The profiler is explicitly prerelease. Keep it centrally versioned and easy to remove if
deployment validation identifies a platform-specific issue.
