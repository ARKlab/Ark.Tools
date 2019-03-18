﻿using Microsoft.ApplicationInsights.Extensibility;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
    /// <summary>
    /// Represents factory used to create <see cref="ITelemetryProcessor"/> with dependency injection support.
    /// </summary>
    public interface ITelemetryProcessorFactory
    {
        /// <summary>
        /// Returns a <see cref="ITelemetryProcessor"/>,
        /// given the next <see cref="ITelemetryProcessor"/> in the call chain.
        /// </summary>
        ITelemetryProcessor Create(ITelemetryProcessor nextProcessor);
    }
}