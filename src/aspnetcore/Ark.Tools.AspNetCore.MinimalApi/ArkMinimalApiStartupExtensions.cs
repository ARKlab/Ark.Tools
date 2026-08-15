// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Ark.Tools.AspNetCore.MinimalApi;

/// <summary>Provides startup diagnostics for Ark Minimal API hosts.</summary>
public static class ArkMinimalApiStartupExtensions
{
    /// <summary>
    /// Enables startup-error capture and detailed hosting diagnostics.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The original web application builder.</returns>
    /// <remarks>
    /// This opt-in helper is intended for Ark's host defaults. Applications that
    /// compose ASP.NET Core directly can omit it and choose their own hosting settings.
    /// </remarks>
    public static WebApplicationBuilder UseArkMinimalApiStartupDiagnostics(
        this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WebHost
            .CaptureStartupErrors(true)
            .UseSetting(WebHostDefaults.DetailedErrorsKey, "true");

        return builder;
    }
}
