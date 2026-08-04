// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Ark.Tools.AspNetCore.HealthChecks;

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

    /// <summary>
    /// Gets or sets whether the host accepts and validates the <c>X-Forwarded-Prefix</c> header.
    /// </summary>
    public bool UseForwardedPrefix { get; set; } = true;
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
        services.AddArkHealthChecks();
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

        var options = app.ApplicationServices.GetService<ArkMinimalApiHostOptions>();
        if (options?.UseForwardedPrefix == true)
        {
            app.Use((context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Forwarded-Prefix", out var values))
                {
                    if (values.Count != 1 || !TryGetForwardedPrefix(values[0], out var prefix))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return Task.CompletedTask;
                    }

                    context.Request.PathBase = new PathString(
                        prefix + context.Request.PathBase.Value);
                }

                return next();
            });
        }

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseSimpleInjector(container);
        return app;
    }

    /// <summary>Maps standard Ark Minimal API endpoints.</summary>
    /// <param name="endpoints">The application endpoint route builder.</param>
    /// <returns>The original endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapArkMinimalApiHost(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapArkHealthChecks();
        return endpoints;
    }

    private static bool TryGetForwardedPrefix(string? value, out string prefix)
    {
        prefix = string.Empty;
        if (string.IsNullOrEmpty(value)
            || value.Length < 2
            || value[0] != '/'
            || value[1] == '/'
            || value[^1] == '/'
            || value.Contains(',', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Contains('?', StringComparison.Ordinal)
            || value.Contains('#', StringComparison.Ordinal)
            || value.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        var segments = value.Split('/');
        for (var segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
        {
            var segment = segments[segmentIndex];
            if (segment.Length == 0 || segment is "." or "..")
            {
                return false;
            }

            for (var index = 0; index < segment.Length; index++)
            {
                if (segment[index] == '%')
                {
                    if (index + 2 >= segment.Length
                        || !Uri.IsHexDigit(segment[index + 1])
                        || !Uri.IsHexDigit(segment[index + 2]))
                    {
                        return false;
                    }

                    index += 2;
                }
                else if (char.IsWhiteSpace(segment[index]) || char.IsControl(segment[index]))
                {
                    return false;
                }
            }

            var decodedSegment = Uri.UnescapeDataString(segment);
            if (decodedSegment is "." or ".."
                || decodedSegment.Contains('/', StringComparison.Ordinal)
                || decodedSegment.Contains('\\', StringComparison.Ordinal)
                || decodedSegment.Any(char.IsWhiteSpace)
                || decodedSegment.Any(char.IsControl))
            {
                return false;
            }
        }

        prefix = value;
        return true;
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
