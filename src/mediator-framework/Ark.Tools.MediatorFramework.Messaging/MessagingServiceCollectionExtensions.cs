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
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessagingCodec, JsonMessagingCodec>());
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
        services.AddSingleton<IMessagingTransport>(transport);
        if (transport is IMessagingReceiveTransport receiveTransport)
            services.AddSingleton(receiveTransport);
        if (transport is IMessagingTransportManagement management)
            services.AddSingleton(management);
        return services;
    }

    /// <summary>Registers the shared DataBus provider.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="dataBus">The provider shared by all network participants.</param>
    /// <param name="networks">The networks that use the provider.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkMessagingDataBus(
        this IServiceCollection services,
        IMessagingDataBus dataBus,
        params MessagingNetworkOptions[] networks)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dataBus);
        ArgumentNullException.ThrowIfNull(networks);
        _validateDataBusLifetime(dataBus, networks);
        services.AddSingleton<IMessagingDataBus>(dataBus);
        return services;
    }

    /// <summary>Registers the in-memory DataBus provider.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="clock">The clock used for attachment expiry.</param>
    /// <param name="lifetime">The attachment lifetime.</param>
    /// <param name="networks">The networks that use the provider.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkInMemoryMessagingDataBus(
        this IServiceCollection services,
        NodaTime.IClock? clock = null,
        NodaTime.Duration? lifetime = null,
        params MessagingNetworkOptions[] networks)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddArkMessagingDataBus(
            new InMemoryMessagingDataBus(
                clock ?? NodaTime.SystemClock.Instance,
                lifetime ?? NodaTime.Duration.FromHours(1)),
            networks);
    }

    /// <summary>Registers the default-lifetime in-memory DataBus provider.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="networks">The networks that use the provider.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkInMemoryMessagingDataBus(
        this IServiceCollection services,
        params MessagingNetworkOptions[] networks)
    {
        return services.AddArkInMemoryMessagingDataBus(
            clock: null,
            lifetime: null,
            networks);
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

    private static void _validateDataBusLifetime(
        IMessagingDataBus dataBus,
        IEnumerable<MessagingNetworkOptions> networks)
    {
        if (dataBus is not InMemoryMessagingDataBus inMemory)
            return;

        foreach (var network in networks)
        {
            ArgumentNullException.ThrowIfNull(network);
            if (network.MaximumSchedulingDelay <= TimeSpan.Zero)
                continue;

            var required = NodaTime.Duration.FromTimeSpan(network.MaximumSchedulingDelay);
            if (inMemory.MinimumAttachmentLifetime <= required)
                throw new ArgumentOutOfRangeException(
                    nameof(dataBus),
                    $"The DataBus attachment lifetime must cover network '{network.NetworkIdentity}' maximum scheduling delay.");
        }
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
