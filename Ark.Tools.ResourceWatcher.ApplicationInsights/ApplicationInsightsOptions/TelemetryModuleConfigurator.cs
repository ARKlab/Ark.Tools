﻿using System;
using Microsoft.ApplicationInsights.Extensibility;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
    /// <summary>
    /// Represents method used to configure <see cref="ITelemetryModule"/> with dependency injection support.
    /// </summary>
    internal class TelemetryModuleConfigurator : ITelemetryModuleConfigurator
    {
        private readonly Action<ITelemetryModule, ApplicationInsightsServiceOptions> configure;

        /// <summary>
        /// Constructs an instance of <see cref="TelemetryModuleConfigurator"/>.
        /// </summary>        
        /// <param name="configure">The action used to configure telemetry module.</param>
        /// <param name="telemetryModuleType">The type of the telemetry module being configured.</param>
        public TelemetryModuleConfigurator(Action<ITelemetryModule, ApplicationInsightsServiceOptions> configure, Type telemetryModuleType)
        {
            this.configure = configure;
            this.TelemetryModuleType = telemetryModuleType;
        }

        [Obsolete("Use Configure(ITelemetryModule telemetryModule, ApplicationInsightsServiceOptions options) instead.", true)]
        public void Configure(ITelemetryModule telemetryModule)
        {
            this.configure?.Invoke(telemetryModule, null);
        }

        /// <summary>
        /// Configures telemetry module.
        /// </summary>
        public void Configure(ITelemetryModule telemetryModule, ApplicationInsightsServiceOptions options)
        {
            this.configure?.Invoke(telemetryModule, options);
        }

        /// <summary>
        /// Gets the type of <see cref="ITelemetryModule"/> to be configured.     
        /// </summary>
        public Type TelemetryModuleType { get; }
    }
}