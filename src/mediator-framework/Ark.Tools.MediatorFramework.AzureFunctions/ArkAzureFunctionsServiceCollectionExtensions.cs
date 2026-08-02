// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using System.Text.Json;

namespace Ark.MediatorFramework.AzureFunctions;

/// <summary>Registers the runtime services used by generated Azure Functions.</summary>
public static class ArkAzureFunctionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Azure Functions mediator runtime services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Azure Functions ASP.NET Core integration requires MVC JSON configuration.")]
    public static IServiceCollection AddArkAzureFunctions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMvc().AddJsonOptions(options => options.JsonSerializerOptions.ConfigureArkDefaults());
        return services;
    }

    /// <summary>
    /// Registers the Azure Functions mediator runtime services and the application container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="container">The application Simple Injector container.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkAzureFunctions(
        this IServiceCollection services,
        Container container)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);
        services.AddSingleton(container);
        return services.AddArkAzureFunctions();
    }
}
