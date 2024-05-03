// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.ApplicationInsights;
using Ark.Tools.AspNetCore.ApplicationInsights;
using Ark.Tools.AspNetCore.ApplicationInsights.Startup;
using Ark.Tools.NLog;

using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;

namespace Ark.Tools.AspNetCore.Startup
{
    internal class ArkStartupBase
    {
        public IConfiguration Configuration { get; }

        public ArkStartupBase(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpContextAccessor();

            services.AddSingleton<ITelemetryInitializer, WebApiUserTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, WebApi4xxAsSuccessTelemetryInitializer>();

            services.ArkApplicationInsightsTelemetry(Configuration);

            services.AddCors();    
        }

        public virtual void Configure(IApplicationBuilder app)
        {
            var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();

            app.Use((context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Forwarded-PathBase", out var pathbase) && pathbase != "/")
                    context.Request.PathBase = pathbase[0] ?? "" + context.Request.PathBase;
                return next();
            });
            
            app.UseSecurityHeaders(o => o.AddDefaultSecurityHeaders().RemoveServerHeader());
            app.UseHsts();

            app.UseStaticFiles();

        }
    }

}
