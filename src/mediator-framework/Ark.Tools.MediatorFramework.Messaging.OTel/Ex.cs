// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry.Metrics;

namespace Ark.Tools.MediatorFramework.Messaging.OTel;

/// <summary>OpenTelemetry registration for the two messaging metric tiers.</summary>
public static class Ex
{
    /// <summary>Adds the always-on operational messaging tier: limit, in-flight, buffered, throttling and lock renewals.</summary>
    /// <param name="builder">The meter provider builder.</param>
    /// <returns>The original meter provider builder.</returns>
    public static MeterProviderBuilder AddArkMessagingInstrumentation(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(OpenTelemetryProcessingMetricsStep.MeterName);
    }

    /// <summary>Adds the opt-in advanced messaging tier and enables its recording.</summary>
    /// <param name="builder">The meter provider builder.</param>
    /// <returns>The original meter provider builder.</returns>
    /// <remarks>
    /// Registering the meter also turns <see cref="MessagingProcessingOptions.AdvancedMetrics"/> on, so
    /// the registered meter is never silently empty.
    /// </remarks>
    public static MeterProviderBuilder AddArkMessagingAdvancedInstrumentation(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        MessagingMetrics.EnableAdvancedMetrics();
        return builder.AddMeter(MessagingMetrics.AdvancedMeterName);
    }
}
