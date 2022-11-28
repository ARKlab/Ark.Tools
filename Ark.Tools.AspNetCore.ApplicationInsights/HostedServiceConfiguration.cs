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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ark.Tools.AspNetCore.ApplicationInsights
{
    public static class HostedServiceConfiguration
    {
        public static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
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

            services.AddApplicationInsightsTelemetry(o =>
            {
                o.ConnectionString = configuration["ApplicationInsights:ConnectionString"]
                    ?? configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
                    ?? $"InstrumentationKey=" + (
                           configuration["ApplicationInsights:InstrumentationKey"]
                        ?? configuration["APPINSIGHTS_INSTRUMENTATIONKEY"]
                        )
                    ;
                o.EnableAdaptiveSampling = false; // enabled below by EnableAdaptiveSamplingWithCustomSettings
                o.EnableHeartbeat = true;
                o.AddAutoCollectedMetricExtractor = true;
                o.RequestCollectionOptions.InjectResponseHeaders = true;
                o.RequestCollectionOptions.TrackExceptions = true;
                o.DeveloperMode = Debugger.IsAttached;
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
        }
    }
}
