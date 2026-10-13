// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Trace;

using Ark.Tools.Compliance;
using Ark.Tools.OTel;

namespace Ark.Tools.ApplicationInsights;

/// <summary>
/// Extension methods for registering the Ark adaptive sampler and telemetry processors
/// with the OpenTelemetry tracing pipeline via the Application Insights SDK v3.x.
/// </summary>
public static class ArkApplicationInsightsExtensions
{
    /// <summary>
    /// Registers the Ark.Tools custom OpenTelemetry sampler and processors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This configures the following pipeline components on top of <c>AddApplicationInsightsTelemetry</c>:
    /// </para>
    /// <list type="bullet">
    /// <item><description><see cref="ArkPreFilterProcessor"/> – drops known-noisy, low-value spans early.</description></item>
    /// <item><description><see cref="ArkAdaptiveSampler"/> – adaptive, per-operation rate-limited sampler with failure preservation.</description></item>
    /// <item><description><see cref="ArkFailurePromotionProcessor"/> – promotes rate-limited spans to exported if they end as failures.</description></item>
    /// <item><description><see cref="ArkTelemetryResourceExtensions.AddArkTelemetryResource"/> – adds the entry assembly as the OpenTelemetry service name.</description></item>
    /// </list>
    /// <para>
    /// Call this method <b>after</b> <c>AddApplicationInsightsTelemetry</c> /
    /// <c>AddApplicationInsightsTelemetryWorkerService</c>.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration used to bind <see cref="ArkAdaptiveSamplerOptions"/>.</param>
    /// <param name="sqlConnectionStringToFilter">
    /// Optional SQL connection string identifying a database whose spans should be filtered out
    /// (e.g. the NLog audit database). If <see langword="null"/>, SQL filtering is disabled.
    /// </param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    [RequiresUnreferencedCode("Binding ArkAdaptiveSamplerOptions from configuration uses reflection.")]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The tracing provider owns and disposes registered processors.")]
    public static IServiceCollection AddArkApplicationInsightsCustomizations(
        this IServiceCollection services,
        IConfiguration configuration,
        [Secret] string? sqlConnectionStringToFilter = null)
    {
        // Configure sampler options with defaults that match the v2.x AdaptiveSampling settings.
        services.Configure<ArkAdaptiveSamplerOptions>(static o =>
        {
            o.TracesPerSecond = 1.0;
            o.MovingAverageRatio = 0.5;
            o.SamplingPercentageDecreaseTimeout = TimeSpan.FromMinutes(1);
            o.EnablePerOperationBucketing = true;
            o.MaxOperationBuckets = 100;
        });

        // Allow override from configuration.
        services.Configure<ArkAdaptiveSamplerOptions>(
            configuration.GetSection("ApplicationInsights:ArkAdaptiveSampler"));

        // Service Bus receives and NLog database writes are dropped before they use the sampler budget.
        var sqlDependencyFilter = new ArkSqlDependencyFilterProcessor(sqlConnectionStringToFilter);
        services.PostConfigure<ArkAdaptiveSamplerOptions>(options =>
        {
            var filter = options.Filter;
            options.Filter = p => !ServiceBusReceiveSamplingFilter.IsServiceBusReceive(p)
                && !sqlDependencyFilter.ShouldDrop(p)
                && (filter is null || filter(p));
        });

        // Register the OTel pipeline customizations via IConfigureOptions<TelemetryConfiguration>.
        // This must run AFTER AddApplicationInsightsTelemetry registers its own IConfigureOptions.
        services.AddSingleton<IConfigureOptions<TelemetryConfiguration>>(sp =>
        {
            var samplerOptions = sp.GetRequiredService<IOptions<ArkAdaptiveSamplerOptions>>().Value;

            // Shared registry for whole-operation failure promotion. The sampler and the
            // failure promotion processor both reference this instance so that a failure
            // detected in one span can immediately influence sampling decisions for new sibling
            // spans and promote in-flight sibling spans when they complete.
            var failedTraceRegistry = new FailedTraceRegistry();

            return new ConfigureNamedOptions<TelemetryConfiguration>(Options.DefaultName, tc =>
            {
                try
                {
                    tc.ConfigureOpenTelemetryBuilder(builder =>
                    {
                        builder.WithTracing(tracerBuilder =>
                        {
                            // Custom adaptive sampler replaces the built-in TracesPerSecond rate limiter.
                            // The filter processors give it back the budget of the spans they drop after sampling.
                            var sampler = new ArkAdaptiveSampler(samplerOptions, failedTraceRegistry);
                            sqlDependencyFilter._sampler = sampler;

                            // Pre-filter processor runs first to drop noisy spans before sampling.
                            tracerBuilder.AddProcessor(new ArkPreFilterProcessor { _sampler = sampler });
                            tracerBuilder.AddArkComplianceRedaction();

                            tracerBuilder.SetSampler(sampler);

                            // Failure promotion: promotes rate-limited spans (and their parent chain /
                            // in-flight siblings) to exported when a failure is detected anywhere in
                            // the operation.
                            tracerBuilder.AddProcessor(new ArkFailurePromotionProcessor(failedTraceRegistry));

                            // SQL dependency filter for the NLog audit database (no-op without a connection string),
                            // for spans whose tags are set after sampling.
                            tracerBuilder.AddProcessor(sqlDependencyFilter);
                        });
                        builder.ConfigureResource(static resource => resource.AddArkTelemetryResource());
                    });
                }
                catch (InvalidOperationException)
                {
                    // The OTel builder may already be finalized when TelemetryConfiguration is
                    // accessed lazily (e.g. after host startup). Silently skip in that case.
                }
            });
        });

        return services;
    }
}
