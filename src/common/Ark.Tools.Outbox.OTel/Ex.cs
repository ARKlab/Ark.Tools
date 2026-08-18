// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry;

namespace Ark.Tools.Outbox.OTel;

/// <summary>
/// OpenTelemetry setup extensions for outbox processors.
/// </summary>
public static class Ex
{
    /// <summary>
    /// Adds outbox processor activities and meters to an exporter-agnostic OpenTelemetry builder.
    /// </summary>
    /// <param name="builder">The OpenTelemetry builder.</param>
    /// <returns>The original OpenTelemetry builder.</returns>
    public static OpenTelemetryBuilder AddArkOutboxOpenTelemetry(this OpenTelemetryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithTracing(tracing => tracing.AddSource(OutboxProcessorBase.InstrumentationName))
            .WithMetrics(metrics => metrics.AddMeter(OutboxProcessorBase.InstrumentationName));
    }
}
