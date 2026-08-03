// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

using Ark.Tools.Solid;
using SimpleInjector;

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Ark.MediatorFramework.AzureFunctions;

/// <summary>Registers the runtime services used by generated Azure Functions.</summary>
public static class ArkAzureFunctionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers application authentication for generated Functions endpoints.
    /// Configure a bearer handler, such as JWT bearer authentication, separately.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">The ASP.NET Core authentication configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkAzureFunctionsBearerAuthentication(
        this IServiceCollection services,
        Action<AuthenticationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (configure is null)
            services.AddAuthentication();
        else
            services.AddAuthentication(configure);
        return services;
    }

    /// <summary>
    /// Registers the explicitly opted-in App Service Easy Auth profile.
    /// The platform authentication switch must be enabled at runtime.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional Ark Functions authentication options.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkAzureFunctionsEasyAuthAuthentication(
        this IServiceCollection services,
        Action<ArkAzureFunctionsAuthenticationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<ArkAzureFunctionsAuthenticationOptions>();
        if (configure is not null)
            services.Configure(configure);
        services.AddAuthentication(options => options.DefaultScheme = "ArkAzureFunctionsEasyAuth")
            .AddScheme<AuthenticationSchemeOptions, ArkAzureFunctionsEasyAuthHandler>(
                "ArkAzureFunctionsEasyAuth",
                _ => { });
        return services;
    }

    /// <summary>
    /// Registers the authentication profile used by generated Functions endpoints.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">The authentication profile configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkAzureFunctionsAuthentication(
        this IServiceCollection services,
        Action<ArkAzureFunctionsAuthenticationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<ArkAzureFunctionsAuthenticationOptions>();
        if (configure is not null)
            services.Configure(configure);
        services.AddAuthentication();
        return services;
    }

    /// <summary>
    /// Registers the Azure Functions mediator runtime services.
    /// Configures HTTP JSON binding with Ark defaults (camelCase, NodaTime, enum-as-member).
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="additionalContexts">
    /// Optional source-generated <see cref="JsonSerializerContext"/> instances to include in the
    /// type-info resolver chain. When provided, types in these contexts are resolved without
    /// reflection. A <see cref="DefaultJsonTypeInfoResolver"/> fallback is always appended.
    /// </param>
    /// <returns>The same service collection.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "DefaultJsonTypeInfoResolver is only used as a fallback for types not covered by the supplied source-generated contexts.")]
    public static IServiceCollection AddArkAzureFunctions(
        this IServiceCollection services,
        params JsonSerializerContext[] additionalContexts)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.ConfigureArkDefaults();
            IJsonTypeInfoResolver resolver = new DefaultJsonTypeInfoResolver();
            if (additionalContexts.Length > 0)
            {
                var resolvers = new IJsonTypeInfoResolver[additionalContexts.Length + 1];
                for (var i = 0; i < additionalContexts.Length; i++)
                    resolvers[i] = additionalContexts[i];
                resolvers[additionalContexts.Length] = new DefaultJsonTypeInfoResolver();
                resolver = JsonTypeInfoResolver.Combine(resolvers);
            }

            options.SerializerOptions.TypeInfoResolver = resolver;
        });

        return services;
    }

    /// <summary>
    /// Registers the Azure Functions mediator runtime services and the application container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="container">The application Simple Injector container.</param>
    /// <param name="additionalContexts">
    /// Optional source-generated <see cref="JsonSerializerContext"/> instances to include in the
    /// type-info resolver chain.
    /// </param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkAzureFunctions(
        this IServiceCollection services,
        Container container,
        params JsonSerializerContext[] additionalContexts)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);
        var httpContextAccessor = new HttpContextAccessor();
        services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);
        services.AddSingleton(container);
        container.RegisterInstance<IContextProvider<ClaimsPrincipal>>(
            new ArkAzureFunctionsUserContextProvider(httpContextAccessor));
        return services.AddArkAzureFunctions(additionalContexts);
    }
}

internal sealed class ArkAzureFunctionsUserContextProvider : IContextProvider<ClaimsPrincipal>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ArkAzureFunctionsUserContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ClaimsPrincipal Current => _httpContextAccessor.HttpContext?.User
        ?? new ClaimsPrincipal(new ClaimsIdentity());
}
