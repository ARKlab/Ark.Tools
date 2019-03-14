using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
    public sealed class ResourceWatcherTelemetryModule : ITelemetryModule, IDisposable
    {
        private ResourceWatcherDiagnosticListener _diagnosticListener;

        public void Dispose()
        {
            ((IDisposable)_diagnosticListener)?.Dispose();
        }

        public void Initialize(TelemetryConfiguration configuration)
        {
            _diagnosticListener = new ResourceWatcherDiagnosticListener(configuration);
        }
    }
}
