using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
    /// <summary>
    /// A telemetry initializer that populates telemetry.Context.Component.Version to the value read from configuration
    /// </summary>
    public class ComponentVersionTelemetryInitializer : ITelemetryInitializer
    {
        private readonly string version;

        public ComponentVersionTelemetryInitializer(IOptions<ApplicationInsightsServiceOptions> options)
        {
            this.version = options.Value.ApplicationVersion;
        }

        public void Initialize(ITelemetry telemetry)
        {
            if (string.IsNullOrEmpty(telemetry.Context.Component.Version))
            {
                if (!string.IsNullOrEmpty(this.version))
                {
                    telemetry.Context.Component.Version = this.version;
                }
            }
        }
    }
}
