// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
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

            // Application Insights
            if (Configuration["ApplicationInsights:InstrumentationKey"] != null)
            {
                services.AddSingleton<ITelemetryInitializer, WebApiUserTelemetryInitializer>();
                services.AddSingleton<ITelemetryInitializer, WebApi4xxAsSuccessTelemetryInitializer>();
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
                

                services.AddSingleton<ITelemetryProcessorFactory, SnapshotCollectorTelemetryProcessorFactory>();
            }

            services.AddCors();            
        }

        public virtual void Configure(IApplicationBuilder app)
        {
            var env = app.ApplicationServices.GetRequiredService<IHostingEnvironment>();

            app.Use(async (context, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    if (context.Response.HasStarted)
                    {
                        throw;
                    }
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json";
                    var json = JToken.FromObject(ex);
                    await context.Response.WriteAsync(json.ToString());
                    throw;
                }
            });

            app.Use((context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Forwarded-PathBase", out var pathbase) && pathbase != "/")
                    context.Request.PathBase = pathbase + context.Request.PathBase;
                return next();
            });

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseStatusCodePages();

            app.UseSecurityHeaders();
            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseCors(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().AllowCredentials());
        }
    }

}
