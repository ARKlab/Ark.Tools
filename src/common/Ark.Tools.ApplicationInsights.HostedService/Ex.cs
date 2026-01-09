using Ark.Tools.AspNetCore.ApplicationInsights;
using Ark.Tools.NLog;

using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using System;
using System.Diagnostics;
using System.Reflection;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.ApplicationInsights.HostedService(net10.0)', Before:
namespace Ark.Tools.ApplicationInsights.HostedService
{
    public static partial class Ex
    {
        public static IHostBuilder AddApplicationInsightsForHostedService(this IHostBuilder builder)
        {
            return builder.ConfigureServices((ctx, services) =>
            {
                services.AddSingleton<ITelemetryInitializer, GlobalInfoTelemetryInitializer>();
                services.AddSingleton<ITelemetryInitializer, DoNotSampleFailures>();

                services.AddApplicationInsightsTelemetryProcessor<ArkSkipUselessSpamTelemetryProcessor>();

                services.Configure<SamplingPercentageEstimatorSettings>(o =>
                {
                    o.MovingAverageRatio = 0.5;
                    o.MaxTelemetryItemsPerSecond = 1;
                    o.SamplingPercentageDecreaseTimeout = TimeSpan.FromMinutes(1);
                });

                services.Configure<SamplingPercentageEstimatorSettings>(o =>
                {
                    ctx.Configuration.GetSection("ApplicationInsights").GetSection("EstimatorSettings").Bind(o);
                });

                // Resolve connection string from configuration
                var connectionString = ctx.Configuration["ApplicationInsights:ConnectionString"]
                    ?? ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
                var instrumentationKey = ctx.Configuration["ApplicationInsights:InstrumentationKey"]
                    ?? ctx.Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];

                // Check if we have a valid connection string or instrumentation key
                var hasValidConnectionString = !string.IsNullOrWhiteSpace(connectionString) || !string.IsNullOrWhiteSpace(instrumentationKey);

                services.AddApplicationInsightsTelemetryWorkerService(o =>
                {
                    o.ApplicationVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();

                    if (hasValidConnectionString)
                    {
                        o.ConnectionString = connectionString ?? $"InstrumentationKey={instrumentationKey}";
                    }
                    else
                    {
                        // When no connection string is provided (e.g., in tests or local development),
                        // use a dummy connection string to prevent SDK errors
                        o.ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";
                        o.DeveloperMode = true;
                    }

                    o.EnableAdaptiveSampling = false; // ENABLED BELOW by ConfigureTelemetryOptions with custom settings
                    o.EnableHeartbeat = true;
                    o.EnableDebugLogger = Debugger.IsAttached || !hasValidConnectionString;
                    o.DeveloperMode ??= Debugger.IsAttached;
                    o.EnableDependencyTrackingTelemetryModule = true;
                });

                services.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, o) => { module.EnableSqlCommandTextInstrumentation = true; });

                // this MUST be after the MS AddApplicationInsightsTelemetry to work. IPostConfigureOptions is NOT working as expected.
                services.AddSingleton<IConfigureOptions<TelemetryConfiguration>, EnableAdaptiveSamplingWithCustomSettings>();

                var cs = ctx.Configuration.GetNLogSetting("ConnectionStrings:" + NLog.NLogDefaultConfigKeys.SqlConnStringName);
                if (!string.IsNullOrWhiteSpace(cs))
                    services.AddSingleton<ITelemetryProcessorFactory>(new SkipSqlDatabaseDependencyFilterFactory(cs!));

            });
        }
=======
namespace Ark.Tools.ApplicationInsights.HostedService;

public static partial class Ex
{
    public static IHostBuilder AddApplicationInsightsForHostedService(this IHostBuilder builder)
    {
        return builder.ConfigureServices((ctx, services) =>
        {
            services.AddSingleton<ITelemetryInitializer, GlobalInfoTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, DoNotSampleFailures>();

            services.AddApplicationInsightsTelemetryProcessor<ArkSkipUselessSpamTelemetryProcessor>();

            services.Configure<SamplingPercentageEstimatorSettings>(o =>
            {
                o.MovingAverageRatio = 0.5;
                o.MaxTelemetryItemsPerSecond = 1;
                o.SamplingPercentageDecreaseTimeout = TimeSpan.FromMinutes(1);
            });

            services.Configure<SamplingPercentageEstimatorSettings>(o =>
            {
                ctx.Configuration.GetSection("ApplicationInsights").GetSection("EstimatorSettings").Bind(o);
            });

            // Resolve connection string from configuration
            var connectionString = ctx.Configuration["ApplicationInsights:ConnectionString"]
                ?? ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            var instrumentationKey = ctx.Configuration["ApplicationInsights:InstrumentationKey"]
                ?? ctx.Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];

            // Check if we have a valid connection string or instrumentation key
            var hasValidConnectionString = !string.IsNullOrWhiteSpace(connectionString) || !string.IsNullOrWhiteSpace(instrumentationKey);

            services.AddApplicationInsightsTelemetryWorkerService(o =>
            {
                o.ApplicationVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();

                if (hasValidConnectionString)
                {
                    o.ConnectionString = connectionString ?? $"InstrumentationKey={instrumentationKey}";
                }
                else
                {
                    // When no connection string is provided (e.g., in tests or local development),
                    // use a dummy connection string to prevent SDK errors
                    o.ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";
                    o.DeveloperMode = true;
                }

                o.EnableAdaptiveSampling = false; // ENABLED BELOW by ConfigureTelemetryOptions with custom settings
                o.EnableHeartbeat = true;
                o.EnableDebugLogger = Debugger.IsAttached || !hasValidConnectionString;
                o.DeveloperMode ??= Debugger.IsAttached;
                o.EnableDependencyTrackingTelemetryModule = true;
            });

            services.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, o) => { module.EnableSqlCommandTextInstrumentation = true; });

            // this MUST be after the MS AddApplicationInsightsTelemetry to work. IPostConfigureOptions is NOT working as expected.
            services.AddSingleton<IConfigureOptions<TelemetryConfiguration>, EnableAdaptiveSamplingWithCustomSettings>();

            var cs = ctx.Configuration.GetNLogSetting("ConnectionStrings:" + NLog.NLogDefaultConfigKeys.SqlConnStringName);
            if (!string.IsNullOrWhiteSpace(cs))
                services.AddSingleton<ITelemetryProcessorFactory>(new SkipSqlDatabaseDependencyFilterFactory(cs!));

        });
>>>>>>> After


namespace Ark.Tools.ApplicationInsights.HostedService;

    public static partial class Ex
    {
        public static IHostBuilder AddApplicationInsightsForHostedService(this IHostBuilder builder)
        {
            return builder.ConfigureServices((ctx, services) =>
            {
                services.AddSingleton<ITelemetryInitializer, GlobalInfoTelemetryInitializer>();
                services.AddSingleton<ITelemetryInitializer, DoNotSampleFailures>();

                services.AddApplicationInsightsTelemetryProcessor<ArkSkipUselessSpamTelemetryProcessor>();

                services.Configure<SamplingPercentageEstimatorSettings>(o =>
                {
                    o.MovingAverageRatio = 0.5;
                    o.MaxTelemetryItemsPerSecond = 1;
                    o.SamplingPercentageDecreaseTimeout = TimeSpan.FromMinutes(1);
                });

                services.Configure<SamplingPercentageEstimatorSettings>(o =>
                {
                    ctx.Configuration.GetSection("ApplicationInsights").GetSection("EstimatorSettings").Bind(o);
                });

                // Resolve connection string from configuration
                var connectionString = ctx.Configuration["ApplicationInsights:ConnectionString"]
                    ?? ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
                var instrumentationKey = ctx.Configuration["ApplicationInsights:InstrumentationKey"]
                    ?? ctx.Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];

                // Check if we have a valid connection string or instrumentation key
                var hasValidConnectionString = !string.IsNullOrWhiteSpace(connectionString) || !string.IsNullOrWhiteSpace(instrumentationKey);

                services.AddApplicationInsightsTelemetryWorkerService(o =>
                {
                    o.ApplicationVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();

                    if (hasValidConnectionString)
                    {
                        o.ConnectionString = connectionString ?? $"InstrumentationKey={instrumentationKey}";
                    }
                    else
                    {
                        // When no connection string is provided (e.g., in tests or local development),
                        // use a dummy connection string to prevent SDK errors
                        o.ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";
                        o.DeveloperMode = true;
                    }

                    o.EnableAdaptiveSampling = false; // ENABLED BELOW by ConfigureTelemetryOptions with custom settings
                    o.EnableHeartbeat = true;
                    o.EnableDebugLogger = Debugger.IsAttached || !hasValidConnectionString;
                    o.DeveloperMode ??= Debugger.IsAttached;
                    o.EnableDependencyTrackingTelemetryModule = true;
                });

                services.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, o) => { module.EnableSqlCommandTextInstrumentation = true; });

                // this MUST be after the MS AddApplicationInsightsTelemetry to work. IPostConfigureOptions is NOT working as expected.
                services.AddSingleton<IConfigureOptions<TelemetryConfiguration>, EnableAdaptiveSamplingWithCustomSettings>();

                var cs = ctx.Configuration.GetNLogSetting("ConnectionStrings:" + NLog.NLogDefaultConfigKeys.SqlConnStringName);
                if (!string.IsNullOrWhiteSpace(cs))
                    services.AddSingleton<ITelemetryProcessorFactory>(new SkipSqlDatabaseDependencyFilterFactory(cs!));

            });
        }
    }