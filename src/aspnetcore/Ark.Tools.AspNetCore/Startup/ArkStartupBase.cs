// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace Ark.Tools.AspNetCore.Startup;

internal sealed class ArkStartupBase
{
    public IConfiguration Configuration { get; }

    internal static readonly string[] _sourceArray = new[] { "/swagger/index.html", "/swagger/oauth2-redirect.html" };

    public ArkStartupBase(IConfiguration configuration)
    {
        Configuration = configuration;
    }
    
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Pattern")]
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddSecurityHeaderPolicies()
            .SetDefaultPolicy(p => p.AddDefaultApiSecurityHeaders().RemoveServerHeader())
            .AddPolicy("Swagger", p =>
            {
                p.AddDefaultSecurityHeaders().RemoveServerHeader();
                p.Remove("Cross-Origin-Opener-Policy");
                p.AddCrossOriginOpenerPolicy(x => x.UnsafeNone());
            })
            .SetPolicySelector(ctx =>
            {
                // yes, contains is a bit dirty but it works for both /swagger and /swagger/index.html even when the path base is not root
                var isSwagger = _sourceArray.Any(x => ctx.HttpContext.Request.Path.Value?.EndsWith(x, System.StringComparison.OrdinalIgnoreCase) == true);

                if (isSwagger)
                    return ctx.ConfiguredPolicies["Swagger"];

                return ctx.DefaultPolicy;
            })
            ;
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Pattern")]
    public void Configure(IApplicationBuilder app)
    {
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