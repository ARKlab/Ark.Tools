// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;

namespace Ark.Tools.AspNetCore.MinimalApiComposition;

/// <summary>Options for the composable Ark Minimal API host integration.</summary>
public sealed class ArkMinimalApiHostOptions
{
    /// <summary>Gets or sets the callback used for application container registrations.</summary>
    public Action<Container>? RegisterContainer { get; set; }

    /// <summary>Gets or sets the callback used for cross-wiring after Microsoft DI is built.</summary>
    public Action<Container, IServiceProvider>? CrossWireContainer { get; set; }

    /// <summary>Gets or sets whether the authorization fallback policy requires authentication.</summary>
    public bool RequireAuthenticatedUser { get; set; } = true;
}

/// <summary>Provides composable Minimal API host defaults.</summary>
public static class ArkMinimalApiHostExtensions
{
    /// <summary>
    /// Adds Ark Minimal API integration, authorization defaults, and startup container verification.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="container">The application SimpleInjector container.</param>
    /// <param name="configure">Optional host integration configuration.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddArkMinimalApiHost(
        this IServiceCollection services,
        Container container,
        Action<ArkMinimalApiHostOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);

        var options = new ArkMinimalApiHostOptions();
        configure?.Invoke(options);

        services.AddHttpContextAccessor();
        services.AddAuthentication();
        services.AddAuthorization(authorization =>
        {
            if (options.RequireAuthenticatedUser)
            {
                authorization.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                authorization.FallbackPolicy = authorization.DefaultPolicy;
            }
        });

        services.AddSimpleInjector(container, simpleInjector =>
        {
            simpleInjector.AddAspNetCore();
            container.Options.ContainerLocking += (_, _) =>
            {
                options.CrossWireContainer?.Invoke(container, simpleInjector.ApplicationServices);
            };
        });

        options.RegisterContainer?.Invoke(container);
        services.AddSingleton<IHostedService>(new SimpleInjectorVerificationHostedService(container));
        return services;
    }

    /// <summary>
    /// Adds SimpleInjector middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="container">The application SimpleInjector container.</param>
    /// <returns>The original application builder.</returns>
    public static IApplicationBuilder UseArkMinimalApiHost(
        this IApplicationBuilder app,
        Container container)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(container);
        return app.UseSimpleInjector(container);
    }

    private sealed class SimpleInjectorVerificationHostedService : IHostedService
    {
        private readonly Container _container;

        public SimpleInjectorVerificationHostedService(Container container)
        {
            _container = container;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _container.Verify();
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }
}
