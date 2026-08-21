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

    /// <summary>Registers a messaging transport and validates network capabilities.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="transport">The transport to register.</param>
    /// <param name="networks">The resolved networks using the transport.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkMessaging(
        this IServiceCollection services,
        IMessagingTransport transport,
        params MessagingNetworkOptions[] networks)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(networks);

        foreach (var network in networks)
        {
            ArgumentNullException.ThrowIfNull(network);
            network.Validate(transport.Capabilities);
        }

        services.AddArkMessaging();
        services.AddSingleton(transport);
        services.AddSingleton<IMessagingTransport>(transport);
        return services;
    }

    /// <summary>Registers the first-class in-memory messaging transport.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="networks">The resolved networks using the transport.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkInMemoryMessaging(
        this IServiceCollection services,
        params MessagingNetworkOptions[] networks)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddArkMessaging(new InMemoryMessagingTransport(), networks);
    }

    /// <summary>Registers the MessagePack and protobuf messaging codecs.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddMessagePackAndProtobufMessagingCodecs(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMessagePackMessagingCodec();
        services.AddProtobufMessagingCodec();
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

        services.AddMessagePackMessagingCodec(resolver);
        services.AddProtobufMessagingCodec();
        return services;
    }

    /// <summary>Registers the MessagePack messaging codec.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddMessagePackMessagingCodec(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IFormatterResolver>(StandardResolver.Instance);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessagingCodec, MessagePackMessagingCodec>());
        return services;
    }

    /// <summary>Registers the MessagePack messaging codec with a host resolver.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="resolver">The host MessagePack formatter resolver.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddMessagePackMessagingCodec(
        this IServiceCollection services,
        IFormatterResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resolver);

        services.Replace(ServiceDescriptor.Singleton<IFormatterResolver>(resolver));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessagingCodec, MessagePackMessagingCodec>());
        return services;
    }

    /// <summary>Registers the protobuf messaging codec.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddProtobufMessagingCodec(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessagingCodec, ProtobufMessagingCodec>());
        return services;
    }
}
