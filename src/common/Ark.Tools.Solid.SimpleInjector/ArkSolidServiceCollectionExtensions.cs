// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Ark.Tools.Solid.SimpleInjector;

/// <summary>
/// Adds Microsoft dependency injection registrations that bridge Ark.Tools.Solid processors to
/// a SimpleInjector container.
/// </summary>
public static class ArkSolidServiceCollectionExtensions
{
    /// <summary>
    /// Registers command, query, and request processors in the service collection by resolving the
    /// SimpleInjector-backed processors from the supplied container.
    /// </summary>
    /// <remarks>
    /// Handler and decorator registration remains owned by SimpleInjector. The registered
    /// processors reuse the active <see cref="AsyncScopedLifestyle"/>
    /// scope when one exists and create one when message-driven execution starts outside an active
    /// scope. Existing Microsoft dependency injection registrations are preserved, so the method is
    /// safe to call from shared composition helpers. SimpleInjector processor registrations may be
    /// completed after this method is called as long as they exist before processor execution.
    /// </remarks>
    /// <param name="services">The application service collection.</param>
    /// <param name="container">The application SimpleInjector container.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddArkSolidProcessors(this IServiceCollection services, Container container)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);

        ScopedProcessorExecution.EnsureSupported(container);
        _tryAddHandlerRegistrationVerifier(services, container);
        _tryAddRequestProcessorBridge(services, container);
        _tryAddQueryProcessorBridge(services, container);
        _tryAddCommandProcessorBridge(services, container);
        return services;
    }

    private static void _tryAddHandlerRegistrationVerifier(IServiceCollection services, Container container)
    {
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(IMediatorHandlerRegistrationVerifier)))
            return;

        services.AddSingleton<IMediatorHandlerRegistrationVerifier>(
            new SimpleInjectorHandlerRegistrationVerifier(container));
    }

    private static void _tryAddRequestProcessorBridge(IServiceCollection services, Container container)
    {
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(IRequestProcessor)))
            return;

        services.AddSingleton<IRequestProcessor>(_ => new ScopeAwareRequestProcessor(container, () => container.GetInstance<IRequestProcessor>()));
    }

    private static void _tryAddQueryProcessorBridge(IServiceCollection services, Container container)
    {
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(IQueryProcessor)))
            return;

        services.AddSingleton<IQueryProcessor>(_ => new ScopeAwareQueryProcessor(container, () => container.GetInstance<IQueryProcessor>()));
    }

    private static void _tryAddCommandProcessorBridge(IServiceCollection services, Container container)
    {
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(ICommandProcessor)))
            return;

        services.AddSingleton<ICommandProcessor>(_ => new ScopeAwareCommandProcessor(container, () => container.GetInstance<ICommandProcessor>()));
    }

    private sealed class SimpleInjectorHandlerRegistrationVerifier(Container container)
        : IMediatorHandlerRegistrationVerifier
    {
        public bool IsRegistered(Type handlerType)
        {
            ArgumentNullException.ThrowIfNull(handlerType);
            return container.GetRegistration(handlerType, throwOnFailure: false) is not null;
        }
    }
}
