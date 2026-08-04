// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using NetEscapades.AspNetCore.SecurityHeaders;

namespace Ark.Tools.AspNetCore.MinimalApi;

/// <summary>Provides the optional Ark Minimal API security profile.</summary>
public static class ArkMinimalApiSecurityExtensions
{
    /// <summary>
    /// Adds the Ark security-header policies used by Minimal API hosts.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddArkMinimalApiSecurity(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSecurityHeaderPolicies()
            .SetDefaultPolicy(policy => policy
                .AddDefaultApiSecurityHeaders()
                .RemoveServerHeader())
            .AddPolicy("Scalar", policy => ConfigureDocumentationPolicy(policy))
            .AddPolicy("Swagger", policy => ConfigureDocumentationPolicy(policy))
            .AddPolicy("GrpcReflection", policy => policy
                .AddDefaultSecurityHeaders()
                .RemoveServerHeader())
            .SetPolicySelector(context =>
            {
                var path = context.HttpContext.Request.Path.Value;

                if (path?.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase) == true
                    || path?.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) == true
                    || path?.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return context.ConfiguredPolicies["Scalar"];
                }

                if (path?.StartsWithSegments("/grpc.reflection", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return context.ConfiguredPolicies["GrpcReflection"];
                }

                return context.DefaultPolicy;
            });

        return services;
    }

    /// <summary>
    /// Adds the Ark security-header middleware and HSTS middleware to the request pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The original application builder.</returns>
    public static IApplicationBuilder UseArkMinimalApiSecurity(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseSecurityHeaders();
        app.UseHsts();
        return app;
    }

    private static void ConfigureDocumentationPolicy(SecurityHeadersPolicy policy)
    {
        policy
            .AddDefaultSecurityHeaders()
            .RemoveServerHeader()
            .Remove("Cross-Origin-Opener-Policy")
            .AddCrossOriginOpenerPolicy(options => options.UnsafeNone());
    }
}
