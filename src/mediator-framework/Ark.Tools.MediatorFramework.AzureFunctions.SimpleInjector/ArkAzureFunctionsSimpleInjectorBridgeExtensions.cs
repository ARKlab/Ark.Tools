// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Security.Claims;

using Ark.Tools.Solid;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SimpleInjector;

namespace Ark.Tools.MediatorFramework.AzureFunctions.SimpleInjector;

/// <summary>
/// Bridges <see cref="Ark.Tools.MediatorFramework.AzureFunctions"/> runtime services that live in
/// Microsoft dependency injection back into the application's SimpleInjector container.
/// </summary>
/// <remarks>
/// <see cref="Ark.Tools.MediatorFramework.AzureFunctions"/> is container-agnostic: it resolves
/// <see cref="IRequestProcessor"/>, <see cref="IQueryProcessor"/>, and <see cref="ICommandProcessor"/>
/// from Microsoft dependency injection (bridge those with
/// <c>Ark.Tools.Solid.SimpleInjector.AddArkSolidProcessors</c>) and registers
/// <see cref="IBus"/>/<see cref="IBusOutboxEnlistment"/>/<see cref="IContextProvider{TItem}"/> of
/// <see cref="ClaimsPrincipal"/> directly in the Microsoft service collection. Application handlers
/// composed with SimpleInjector need those services surfaced inside their own container: this class
/// provides that opt-in cross-wiring.
/// </remarks>
public static class ArkAzureFunctionsSimpleInjectorBridgeExtensions
{
    /// <summary>
    /// Registers a bridge that exposes the Azure Functions <see cref="IBus"/>,
    /// <see cref="IBusOutboxEnlistment"/>, and <see cref="IContextProvider{TItem}"/> of
    /// <see cref="ClaimsPrincipal"/> Microsoft dependency injection registrations into the supplied
    /// SimpleInjector container.
    /// </summary>
    /// <remarks>
    /// Call this after <c>AddArkAzureFunctions</c> and, when hosting native messaging,
    /// <c>AddArkMessagingFunctionsHost</c>/<c>ConfigureArkMessagingFunctions</c>, so the bridge can
    /// detect which Microsoft dependency injection registrations exist. The SimpleInjector
    /// registrations are lazy: the underlying Microsoft dependency injection service provider is
    /// captured once the generic host starts, which is always before Azure Functions serves
    /// requests. The bridge is registered as an <see cref="IHostedService"/> purely to hook into
    /// that startup moment: the generic host resolves every <see cref="IHostedService"/> before it
    /// starts serving requests, which gives the bridge factory delegate above a guaranteed point to
    /// capture the root <see cref="IServiceProvider"/>. <see cref="IHostedService.StartAsync"/>/
    /// <see cref="IHostedService.StopAsync"/> themselves have no work to do.
    /// </remarks>
    /// <param name="services">The Functions service collection.</param>
    /// <param name="container">The application SimpleInjector container.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkAzureFunctionsSimpleInjectorBridge(
        this IServiceCollection services,
        Container container)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);

        var bridge = new AzureFunctionsSimpleInjectorBridge();

        if (services.Any(static service => service.ServiceType == typeof(IContextProvider<ClaimsPrincipal>)))
            container.RegisterSingleton<IContextProvider<ClaimsPrincipal>>(() => bridge.GetContextProvider());

        if (services.Any(static service => service.ServiceType == typeof(IBus)))
            container.RegisterSingleton<IBus>(() => bridge.GetBus());

        if (services.Any(static service => service.ServiceType == typeof(IBusOutboxEnlistment)))
            container.RegisterSingleton<IBusOutboxEnlistment>(() => bridge.GetEnlistment());

        services.AddSingleton<IHostedService>(serviceProvider =>
        {
            bridge.SetServiceProvider(serviceProvider);
            return bridge;
        });
        return services;
    }

    private sealed class AzureFunctionsSimpleInjectorBridge : IHostedService
    {
        private IServiceProvider? _serviceProvider;

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IContextProvider<ClaimsPrincipal> GetContextProvider()
        {
            return _resolvedServiceProvider().GetRequiredService<IContextProvider<ClaimsPrincipal>>();
        }

        public IBus GetBus()
        {
            return _resolvedServiceProvider().GetRequiredService<IBus>();
        }

        public IBusOutboxEnlistment GetEnlistment()
        {
            return _resolvedServiceProvider().GetRequiredService<IBusOutboxEnlistment>();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private IServiceProvider _resolvedServiceProvider()
        {
            return _serviceProvider
                ?? throw new InvalidOperationException("The Functions service provider is not initialized.");
        }
    }
}
