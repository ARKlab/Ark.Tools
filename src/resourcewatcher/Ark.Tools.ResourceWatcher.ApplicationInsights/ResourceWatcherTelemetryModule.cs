using Microsoft.ApplicationInsights.Extensibility;

using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.ResourceWatcher.ApplicationInsights(net10.0)', Before:
namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
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
=======
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
>>>>>>> After


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