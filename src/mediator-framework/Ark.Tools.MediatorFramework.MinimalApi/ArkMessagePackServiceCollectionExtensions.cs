// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using MessagePack;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>Registers MessagePack support for generated Minimal API endpoints.</summary>
public static class ArkMessagePackServiceCollectionExtensions
{
    /// <summary>
    /// Registers the MessagePack resolver used by generated Minimal API endpoints and builds
    /// their serializer options once. Hosts with MessagePack endpoints must call it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="resolver">A resolver that can format every MessagePack contract.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkMessagePack(this IServiceCollection services, IFormatterResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resolver);

        services.Replace(ServiceDescriptor.Singleton<IFormatterResolver>(resolver));
        services.Replace(ServiceDescriptor.Singleton(new ArkMessagePackOptionsHolder(resolver)));
        return services;
    }
}
