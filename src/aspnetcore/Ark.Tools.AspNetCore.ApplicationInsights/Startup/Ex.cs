// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.ApplicationInsights;
using Ark.Tools.NLog;

using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Diagnostics;
using System.Reflection;

namespace Ark.Tools.AspNetCore.ApplicationInsights.Startup;

public static partial class Ex
{
    [RequiresUnreferencedCode("Application Insights configuration binding uses reflection. Configuration types and their properties may be trimmed.")]
    public static IServiceCollection ArkApplicationInsightsTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolve connection string from configuration
        var connectionString = configuration["ApplicationInsights:ConnectionString"]
            ?? configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        var instrumentationKey = configuration["ApplicationInsights:InstrumentationKey"]
            ?? configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];

        // Check if we have a valid connection string or instrumentation key
        var hasValidConnectionString = !string.IsNullOrWhiteSpace(connectionString) || !string.IsNullOrWhiteSpace(instrumentationKey);

        services.AddApplicationInsightsTelemetry(o =>
        {
            if (hasValidConnectionString)
            {
                o.ConnectionString = connectionString ?? $"InstrumentationKey={instrumentationKey}";
            }
            else
            {
                // When no connection string is provided (e.g., in tests or local development),
                // use a dummy connection string
                o.ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";
            }

            o.ApplicationVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
        });

        // Register the Ark adaptive sampler and custom processors on the OTel tracing pipeline.
        // This MUST be after AddApplicationInsightsTelemetry to ensure ordering of IConfigureOptions.
        var sqlCs = configuration.GetNLogSetting("ConnectionStrings:" + NLogDefaultConfigKeys.SqlConnStringName);
        services.AddArkApplicationInsightsCustomizations(configuration, sqlCs);

        services.Configure<SnapshotCollectorConfiguration>(o =>
        {
        });
        services.Configure<SnapshotCollectorConfiguration>(configuration.GetSection(nameof(SnapshotCollectorConfiguration)));
        services.AddSnapshotCollector();

        return services;
    }
}