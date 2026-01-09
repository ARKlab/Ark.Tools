// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.ApplicationInsights;
using Ark.Tools.NLog;

using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using System.Diagnostics;
using System.Reflection;

namespace Ark.Tools.AspNetCore.ApplicationInsights.Startup;

public static partial class Ex
{
    public static IServiceCollection ArkApplicationInsightsTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApplicationInsightsTelemetryProcessor<ArkSkipUselessSpamTelemetryProcessor>();
        services.AddSingleton<ITelemetryInitializer, GlobalInfoTelemetryInitializer>();

        services.AddSingleton<ITelemetryInitializer, DoNotSampleFailures>();

        services.Configure<SamplingPercentageEstimatorSettings>(o =>
        {
            o.MovingAverageRatio = 0.5;
            o.MaxTelemetryItemsPerSecond = 1;
            o.SamplingPercentageDecreaseTimeout = TimeSpan.FromMinutes(1);
        });

        services.Configure<SamplingPercentageEstimatorSettings>(o =>
        {
            configuration.GetSection("ApplicationInsights:EstimatorSettings").Bind(o);
        });

        // Resolve connection string from configuration
        var connectionString = configuration["ApplicationInsights:ConnectionString"]
            ?? configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        var instrumentationKey = configuration["ApplicationInsights:InstrumentationKey"]
            ?? configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];

        // Check if we have a valid connection string or instrumentation key
        var hasValidConnectionString = !string.IsNullOrWhiteSpace(connectionString) || !string.IsNullOrWhiteSpace(instrumentationKey);

        // Check if we're in a test environment or debugger is attached
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var isIntegrationTests = string.Equals(environment, "IntegrationTests", StringComparison.OrdinalIgnoreCase);
        var useInMemoryChannel = isIntegrationTests || Debugger.IsAttached;

        // In test environments, use InMemoryChannel to prevent telemetry transmission crashes
        // See: https://github.com/microsoft/ApplicationInsights-dotnet/issues/2322
        if (useInMemoryChannel || !hasValidConnectionString)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope - DI container manages lifetime
            services.AddSingleton<Microsoft.ApplicationInsights.Channel.ITelemetryChannel>(
                new Microsoft.ApplicationInsights.Channel.InMemoryChannel { DeveloperMode = true });
#pragma warning restore CA2000
        }

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

            o.EnableAdaptiveSampling = false; // enabled below by EnableAdaptiveSamplingWithCustomSettings
            o.EnableHeartbeat = !useInMemoryChannel; // Disable heartbeat in tests to prevent background tasks
            o.AddAutoCollectedMetricExtractor = true;
            o.RequestCollectionOptions.InjectResponseHeaders = true;
            o.RequestCollectionOptions.TrackExceptions = true;
            o.DeveloperMode ??= isIntegrationTests || Debugger.IsAttached;
            o.EnableDebugLogger = Debugger.IsAttached || !hasValidConnectionString;
            o.ApplicationVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
        });

        services.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, o) => { module.EnableSqlCommandTextInstrumentation = true; });

        // this MUST be after the MS AddApplicationInsightsTelemetry to work. IPostConfigureOptions is NOT working as expected.
        services.AddSingleton<IConfigureOptions<TelemetryConfiguration>, EnableAdaptiveSamplingWithCustomSettings>();

        var cs = configuration.GetNLogSetting("ConnectionStrings:" + NLogDefaultConfigKeys.SqlConnStringName);
        if (!string.IsNullOrWhiteSpace(cs))
            services.AddSingleton<ITelemetryProcessorFactory>(
                new SkipSqlDatabaseDependencyFilterFactory(cs));

        services.Configure<SnapshotCollectorConfiguration>(o =>
        {
        });
        services.Configure<SnapshotCollectorConfiguration>(configuration.GetSection(nameof(SnapshotCollectorConfiguration)));
        services.AddSnapshotCollector();

        return services;
    }
}