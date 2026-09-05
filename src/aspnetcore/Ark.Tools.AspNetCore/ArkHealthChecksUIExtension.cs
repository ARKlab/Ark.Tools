// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using HealthChecks.UI.Configuration;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.AspNetCore.HealthChecks;

/// <summary>Provides extensions for the Ark Health Checks UI.</summary>
public static class ArkHealthChecksUIExtension
{
    /// <summary>Adds the Ark Health Checks UI services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddArkHealthChecksUI(this IServiceCollection services)
    {
        services.AddHealthChecksUI(setupSettings: static setup =>
        {
            setup.SetEvaluationTimeInSeconds(60);
            setup.MaximumHistoryEntriesPerEndpoint(50);
            setup.AddHealthCheckEndpoint("Health Checks", "/healthCheck");
        }
        ).AddInMemoryStorage();

        services.AddArkHealthChecksUIOptions(static o =>
        {
            if (File.Exists(Path.Join(Environment.CurrentDirectory, "UIHealthChecks.css")))
                o.AddCustomStylesheet("UIHealthChecks.css");
            var binPath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "UIHealthChecks.css");
            if (File.Exists(binPath))
                o.AddCustomStylesheet(binPath);
        });

        return services;
    }

    /// <summary>Configures the Ark Health Checks UI.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="setup">The UI configuration action.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddArkHealthChecksUIOptions(this IServiceCollection services, Action<Options> setup)
    {
        return services.AddSingleton(setup);
    }

    /// <summary>Maps the Ark Health Checks UI endpoints.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapArkHealthChecksUI(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecksUI(setup =>
        {
            var configurers = endpoints.ServiceProvider.GetServices<Action<Options>>();
            foreach (var c in configurers)
                c?.Invoke(setup);
        });

        return endpoints;
    }
}
