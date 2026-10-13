// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.Configuration;

using OpenTelemetry;
using OpenTelemetry.Instrumentation.SqlClient;
using OpenTelemetry.Trace;

using Ark.Tools.AspNetCore.HealthChecks.OTel;
using Ark.Tools.OTel;

namespace Ark.Tools.AspNetCore.OTel;

/// <summary>
/// OpenTelemetry setup extensions for Ark ASP.NET Core hosts.
/// </summary>
public static class Ex
{
    private const string _suppressActivityOpenTelemetryDataSwitch = "Microsoft.AspNetCore.Hosting.SuppressActivityOpenTelemetryData";

    /// <summary>
    /// Adds the Ark OpenTelemetry pipeline (see <see cref="ArkOpenTelemetryBuilderExtensions.AddArkOpenTelemetry"/>)
    /// and the health check metrics to an exporter-agnostic OpenTelemetry builder.
    /// </summary>
    /// <param name="builder">The OpenTelemetry builder.</param>
    /// <param name="configureSqlClient">
    /// Optional application configuration applied after Ark SQL defaults.
    /// </param>
    /// <param name="configureArkOtel">
    /// Optional application configuration for Ark OpenTelemetry instrumentation.
    /// </param>
    /// <param name="configureAdaptiveSampler">
    /// Optional application configuration for the adaptive sampler.
    /// </param>
    /// <param name="configuration">
    /// Optional application configuration, see
    /// <see cref="ArkOpenTelemetryBuilderExtensions.AddArkOpenTelemetry"/>.
    /// </param>
    /// <returns>The original OpenTelemetry builder.</returns>
    public static OpenTelemetryBuilder AddArkAspNetCoreOpenTelemetry(
        this OpenTelemetryBuilder builder,
        Action<SqlClientTraceInstrumentationOptions>? configureSqlClient = null,
        Action<ArkOtelConfig>? configureArkOtel = null,
        Action<ArkAdaptiveSamplerOptions>? configureAdaptiveSampler = null,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // ASP.NET Core 11 passes the request tags when it creates the server span, so the sampler can drop noise
        // such as OPTIONS requests before it uses its budget. Do the same on earlier runtimes; an explicit
        // application choice wins.
        if (Environment.Version.Major < 11 && !AppContext.TryGetSwitch(_suppressActivityOpenTelemetryDataSwitch, out _))
            AppContext.SetSwitch(_suppressActivityOpenTelemetryDataSwitch, false);

        builder.Services.AddArkOTelHealthCheckPublisher();

        return builder
            .AddArkOpenTelemetry(configureSqlClient, configureArkOtel, configureAdaptiveSampler, configuration)
            .WithTracing(static tracing => tracing.AddAspNetCoreInstrumentation())
            .WithMetrics(static metrics => metrics
                // Native ASP.NET Core request metrics, as the Azure Monitor Distro collects them.
                .AddMeter("Microsoft.AspNetCore.Hosting")
                .AddMeter(ArkOTelHealthCheckPublisher.MeterName));
    }

}
