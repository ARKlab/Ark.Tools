// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SimpleInjector;

namespace Ark.Tools.AspNetCore.MinimalApi;

/// <summary>Options for the Ark Minimal API host integration.</summary>
public sealed class ArkMinimalApiHostOptions
{
    /// <summary>Gets or sets the callback used for application container registrations.</summary>
    public Action<Container>? RegisterContainer { get; set; }

    /// <summary>Gets or sets the callback used for cross-wiring after Microsoft DI is built.</summary>
    public Action<Container, IServiceProvider>? CrossWireContainer { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked after the container has been verified and before the
    /// server starts accepting requests. Use it to start application resources (for example a bus).
    /// </summary>
    public Action<Container>? OnContainerVerified { get; set; }

    /// <summary>
    /// Gets or sets whether the authorization default and fallback policies require an
    /// authenticated user.
    /// </summary>
    public bool RequireAuthenticatedUser { get; set; } = true;
}

/// <summary>Provides composable Minimal API host defaults.</summary>
public static class ArkMinimalApiHostExtensions
{
    /// <summary>
    /// Adds Ark Minimal API integration and authorization defaults.
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
        services.AddSingleton(options);
        services.AddSingleton<IHostedService>(_ => new SimpleInjectorVerificationHostedService(container, options));
        return services;
    }

    /// <summary>
    /// Adds required Minimal API and SimpleInjector middleware to the application pipeline.
    /// </summary>
    /// <remarks>
    /// This method adds routing, authentication, authorization, and SimpleInjector middleware.
    /// Container verification and application startup callbacks run in a hosted service during
    /// <see cref="IHostedService.StartAsync(CancellationToken)"/>, before the server starts
    /// accepting requests. The container lifetime remains owned by the application.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <param name="container">The application SimpleInjector container.</param>
    /// <returns>The original application builder.</returns>
    public static IApplicationBuilder UseArkMinimalApiHost(
        this IApplicationBuilder app,
        Container container)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(container);

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseSimpleInjector(container);
        return app;
    }

    private sealed class SimpleInjectorVerificationHostedService : IHostedService
    {
        private readonly Container _container;
        private readonly ArkMinimalApiHostOptions _options;

        public SimpleInjectorVerificationHostedService(Container container, ArkMinimalApiHostOptions options)
        {
            _container = container;
            _options = options;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _container.Verify();
            _options.OnContainerVerified?.Invoke(_container);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
