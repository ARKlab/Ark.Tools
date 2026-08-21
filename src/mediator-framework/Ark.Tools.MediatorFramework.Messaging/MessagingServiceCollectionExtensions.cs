// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;

using MessagePack;
using MessagePack.Resolvers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        services.AddOptions<JsonSerializerOptions>();
        services.AddSingleton<IMessagingCodec, JsonMessagingCodec>();
        services.AddSingleton<MessagingCodecRegistry>();
        services.AddSingleton<IMessagingCodecRegistry>(
            serviceProvider => serviceProvider.GetRequiredService<MessagingCodecRegistry>());
        return services;
    }

    /// <summary>Registers the MessagePack and protobuf messaging codecs.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddMessagePackAndProtobufMessagingCodecs(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IFormatterResolver>(StandardResolver.Instance);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessagingCodec, MessagePackMessagingCodec>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessagingCodec, ProtobufMessagingCodec>());
        return services;
    }

    /// <summary>Registers the MessagePack and protobuf codecs with a host resolver.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="resolver">The host MessagePack formatter resolver.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddMessagePackAndProtobufMessagingCodecs(
        this IServiceCollection services,
        IFormatterResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resolver);

        services.Replace(ServiceDescriptor.Singleton<IFormatterResolver>(resolver));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessagingCodec, MessagePackMessagingCodec>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessagingCodec, ProtobufMessagingCodec>());
        return services;
    }
}
