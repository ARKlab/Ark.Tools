// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.ApplicationInsights;
using Ark.Tools.AspNetCore.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
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

            services.AddApplicationInsightsTelemetryProcessor<ArkSkipUselessSpamTelemetryProcessor>();
            services.AddSingleton<ITelemetryInitializer, WebApiUserTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, WebApi4xxAsSuccessTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, GlobalInfoTelemetryInitializer>();
            
            services.AddApplicationInsightsTelemetry(o =>
            {
                o.InstrumentationKey = Configuration["ApplicationInsights:InstrumentationKey"];
                o.EnableAdaptiveSampling = true;
                o.EnableHeartbeat = true;
                o.AddAutoCollectedMetricExtractor = true;
                o.RequestCollectionOptions.InjectResponseHeaders = true;
                o.RequestCollectionOptions.TrackExceptions = true;
                o.DeveloperMode = Debugger.IsAttached;
                o.ApplicationVersion = FileVersionInfo.GetVersionInfo(this.GetType().Assembly.Location).FileVersion;
            });
            services.AddSingleton<ITelemetryProcessorFactory>(new SkipSqlDatabaseDependencyFilterFactory(Configuration.GetConnectionString(NLog.NLogDefaultConfigKeys.SqlConnStringName)));

            services.Configure<SnapshotCollectorConfiguration>(o =>
            {
            });
            services.Configure<SnapshotCollectorConfiguration>(Configuration.GetSection(nameof(SnapshotCollectorConfiguration)));
            services.AddSnapshotCollector();

            services.AddCors();
            
        }

        public virtual void Configure(IApplicationBuilder app)
        {
            var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
                        
            app.Use((context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Forwarded-PathBase", out var pathbase) && pathbase != "/")
                    context.Request.PathBase = pathbase + context.Request.PathBase;
                return next();
            });
            
            app.UseSecurityHeaders(o => o.AddDefaultSecurityHeaders().RemoveServerHeader());
            app.UseHsts();

            app.UseStaticFiles();

        }
    }

}
