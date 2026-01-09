using Microsoft.ApplicationInsights.Extensibility;

using System;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights;

public sealed class ResourceWatcherTelemetryModule : ITelemetryModule, IDisposable
{
    private ResourceWatcherDiagnosticListener? _diagnosticListener;

    public void Dispose()
    {
        _diagnosticListener?.Dispose();
    }

    public void Initialize(TelemetryConfiguration configuration)
    {
        _diagnosticListener = new ResourceWatcherDiagnosticListener(configuration);
    }
}
