// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

using Ark.Tools.Solid;

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Ark.Tools.MediatorFramework.AzureFunctions;

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
        services.AddAuthentication(static options => options.DefaultScheme = "ArkAzureFunctionsEasyAuth")
            .AddScheme<AuthenticationSchemeOptions, ArkAzureFunctionsEasyAuthHandler>(
                "ArkAzureFunctionsEasyAuth",
                static _ => { });
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
    /// reflection. A <see cref="DefaultJsonTypeInfoResolver"/> fallback is appended while reflection-based
    /// serialization is enabled.
    /// </param>
    /// <returns>The same service collection.</returns>
    /// <remarks>
    /// In a trimmed app (<see cref="JsonSerializer.IsReflectionEnabledByDefault"/> is
    /// <see langword="false"/>), the reflection-based Ark default converters and the fallback resolver are not
    /// added: only the supplied contexts resolve types, so they must cover every contract and response type and
    /// declare the converters they need, for example in <see cref="JsonSourceGenerationOptionsAttribute.Converters"/>.
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "The reflection-based defaults and DefaultJsonTypeInfoResolver are guarded by the JsonSerializer.IsReflectionEnabledByDefault feature switch, which trimming disables.")]
    public static IServiceCollection AddArkAzureFunctions(
        this IServiceCollection services,
        params JsonSerializerContext[] additionalContexts)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.TryAddSingleton<IContextProvider<ClaimsPrincipal>, ArkAzureFunctionsUserContextProvider>();

        services.ConfigureHttpJsonOptions(options =>
        {
            if (!JsonSerializer.IsReflectionEnabledByDefault)
            {
                // Trimmed: keep the reflection-free part of the Ark defaults; the supplied
                // source-generated contexts resolve every type.
                options.SerializerOptions.AllowTrailingCommas = true;
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                options.SerializerOptions.PropertyNameCaseInsensitive = true;
                options.SerializerOptions.Converters.Add(new Ark.Tools.SystemTextJson.ValueObjectJsonConverterFactory());
                if (additionalContexts.Length > 0)
                    options.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(additionalContexts);
                return;
            }

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
        services.AddHealthChecks();

        return services;
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
