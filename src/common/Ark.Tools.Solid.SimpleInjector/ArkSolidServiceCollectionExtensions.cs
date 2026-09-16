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
    /// existing SimpleInjector-backed processors from the supplied container.
    /// </summary>
    /// <remarks>
    /// Handler and decorator registration remains owned by SimpleInjector. The registered
    /// processors reuse the active <see cref="AsyncScopedLifestyle"/>
    /// scope when one exists and create one when message-driven execution starts outside an active
    /// scope.
    /// </remarks>
    /// <param name="services">The application service collection.</param>
    /// <param name="container">The application SimpleInjector container.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddArkSolidProcessors(this IServiceCollection services, Container container)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);

        ScopedProcessorExecution.EnsureSupported(container);
        services.AddSingleton<IRequestProcessor>(_ => new ScopeAwareRequestProcessor(container, container.GetInstance<IRequestProcessor>()));
        services.AddSingleton<IQueryProcessor>(_ => new ScopeAwareQueryProcessor(container, container.GetInstance<IQueryProcessor>()));
        services.AddSingleton<ICommandProcessor>(_ => new ScopeAwareCommandProcessor(container, container.GetInstance<ICommandProcessor>()));
        return services;
    }
}
