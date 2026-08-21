// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Registers the transport-neutral messaging runtime.</summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the host-options-driven JSON codec and codec registry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkMessaging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<JsonOptions>();
        services.AddSingleton<IMessagingCodec, JsonMessagingCodec>();
        services.AddSingleton<MessagingCodecRegistry>();
        services.AddSingleton<IMessagingCodecRegistry>(
            serviceProvider => serviceProvider.GetRequiredService<MessagingCodecRegistry>());
        return services;
    }
}
