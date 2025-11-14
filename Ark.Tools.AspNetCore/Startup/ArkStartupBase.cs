// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.ApplicationInsights;
using Ark.Tools.AspNetCore.ApplicationInsights.Startup;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Linq;

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

            services.AddSecurityHeaderPolicies()
                .SetDefaultPolicy(p => p.AddDefaultApiSecurityHeaders().RemoveServerHeader())
                .AddPolicy("Swagger", p => {
                    p.AddDefaultSecurityHeaders().RemoveServerHeader();
                    p.Remove("Cross-Origin-Opener-Policy");
                    p.AddCrossOriginOpenerPolicy(x => x.UnsafeNone());
                })
                .SetPolicySelector(ctx =>
                {
                    // yes, contains is a bit dirty but it works for both /swagger and /swagger/index.html even when the path base is not root
                    var isSwagger = new[] { "/swagger/index.html", "/swagger/oauth2-redirect.html" }
                        .Any(x => ctx.HttpContext.Request.Path.Value?.EndsWith(x, System.StringComparison.OrdinalIgnoreCase) == true);

                    if (isSwagger)
                        return ctx.ConfiguredPolicies["Swagger"];

                    return ctx.DefaultPolicy;
                })
                ;
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

            app.UseSecurityHeaders();
            app.UseHsts();

            app.UseStaticFiles();

        }

    }

}
