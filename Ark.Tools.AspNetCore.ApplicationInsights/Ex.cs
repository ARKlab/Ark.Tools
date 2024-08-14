﻿// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.Extensions.Hosting;
using Ark.Tools.AspNetCore.ApplicationInsights.Startup;

namespace Ark.Tools.AspNetCore.ApplicationInsights
{
    public static partial class Ex
    {
        public static IHostBuilder AddApplicationInsithsTelemetryForWebHostArk(this IHostBuilder builder)
        {
            return builder.ConfigureServices((ctx, services) =>
            {
                services.ArkApplicationInsightsTelemetry(ctx.Configuration);
            });
        }

    }
}
