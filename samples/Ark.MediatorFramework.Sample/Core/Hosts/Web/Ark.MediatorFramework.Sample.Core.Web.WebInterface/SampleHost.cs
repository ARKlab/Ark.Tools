// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application;
using Ark.Tools.AspNetCore.MinimalApi;
using Ark.Tools.AspNetCore.OTel;
using Ark.Tools.MediatorFramework;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.MediatorFramework.Messaging.OTel;
using Ark.Tools.NLog;

namespace Ark.MediatorFramework.Sample.Core.Web.WebInterface;

/// <summary>Provides the sample's production host composition seam.</summary>
public static class SampleHost
{
    /// <summary>
    /// Applies the production host registrations to a web application builder.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="container">The application dependency injection container.</param>
    /// <param name="transport">The messaging transport.</param>
    /// <param name="dataBus">The claim-check DataBus.</param>
    /// <param name="resourceManagement">
    /// Provisions the producer's resources; <see langword="null"/> when the transport manages its own.
    /// </param>
    /// <returns>The startup configuration used to complete application wiring.</returns>
    public static SampleStartup Configure(
        WebApplicationBuilder builder,
        SimpleInjector.Container container,
        IMessagingTransport transport,
        IMessagingDataBus dataBus,
        IMessagingTransportManagement? resourceManagement)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(dataBus);

        builder.UseArkMinimalApiStartupDiagnostics();
        builder.Host.ConfigureNLog("Ark.MediatorFramework.Sample.Core.Web.WebInterface");

        builder.Services.AddArkAzureMonitorOpenTelemetry(builder.Configuration);

        // The advanced tier is a tuning profile: opt in from configuration, not by default.
        var advancedMetrics = builder.Configuration.GetValue<bool>("Messaging:AdvancedMetrics");
        builder.Services.AddOpenTelemetry()
            .WithTracing(static tracing => tracing.AddSource(SampleTelemetry.ActivitySourceName))
            .WithMetrics(metrics =>
            {
                metrics.AddArkMessagingInstrumentation();
                if (advancedMetrics)
                    metrics.AddArkMessagingAdvancedInstrumentation();
            });
        var startup = new SampleStartup(
            container,
            transport,
            dataBus,
            resourceManagement,
            builder.Environment,
            builder.Configuration);
        startup.ConfigureServices(builder.Services);
        return startup;
    }
}
