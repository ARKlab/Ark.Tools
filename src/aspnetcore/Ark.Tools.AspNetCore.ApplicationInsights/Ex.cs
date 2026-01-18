// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.ApplicationInsights.Startup;

using Microsoft.Extensions.Hosting;

namespace Ark.Tools.AspNetCore.ApplicationInsights;

public static partial class Ex
{
    [RequiresUnreferencedCode("Application Insights configuration binding uses reflection. Configuration types and their properties may be trimmed.")]
    public static IHostBuilder AddApplicationInsithsTelemetryForWebHostArk(this IHostBuilder builder)
    {
        return builder.ConfigureServices((ctx, services) =>
        {
            services.ArkApplicationInsightsTelemetry(ctx.Configuration);
        });
    }

}