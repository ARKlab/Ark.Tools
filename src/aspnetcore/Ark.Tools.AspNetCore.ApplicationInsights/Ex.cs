// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.ApplicationInsights.Startup;

using Microsoft.Extensions.Hosting;

namespace Ark.Tools.AspNetCore.ApplicationInsights;

public static partial class Ex
{
    /// <summary>
    /// Adds Application Insights telemetry for an ASP.NET Core web host.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The configured host builder.</returns>
    [RequiresUnreferencedCode("Application Insights configuration binding uses reflection. Configuration types and their properties may be trimmed.")]
    public static IHostBuilder AddApplicationInsightsTelemetryForWebHostArk(this IHostBuilder builder)
    {
        return builder.AddApplicationInsithsTelemetryForWebHostArk();
    }

    [RequiresUnreferencedCode("Application Insights configuration binding uses reflection. Configuration types and their properties may be trimmed.")]
    public static IHostBuilder AddApplicationInsithsTelemetryForWebHostArk(this IHostBuilder builder)
    {
        return builder.ConfigureServices((ctx, services) =>
        {
            services.ArkApplicationInsightsTelemetry(ctx.Configuration);
        });
    }

}