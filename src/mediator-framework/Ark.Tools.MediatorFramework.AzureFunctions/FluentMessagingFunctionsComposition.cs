// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Reflection;

using Ark.Tools.MediatorFramework.Messaging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SimpleInjector;

namespace Ark.Tools.MediatorFramework.AzureFunctions;

/// <summary>Provides fluent Azure Functions messaging composition.</summary>
public static class FluentMessagingFunctionsCompositionExtensions
{
    /// <summary>Configures one generated Azure Functions messaging host.</summary>
    /// <typeparam name="TNetwork">The generated messaging network declaration.</typeparam>
    /// <typeparam name="TParticipant">The generated participant declaration.</typeparam>
    /// <typeparam name="THost">The generated Functions host declaration.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="container">The application Simple Injector container.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="configure">The Functions composition callback.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection ConfigureArkMessagingFunctions<
        TNetwork,
        TParticipant,
        THost>(
        this IServiceCollection services,
        Container container,
        IConfiguration configuration,
        Action<MessagingFunctionsCompositionBuilder<TNetwork, TParticipant, THost>> configure)
        where TNetwork : class
        where TParticipant : class
        where THost : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new MessagingFunctionsCompositionBuilder<TNetwork, TParticipant, THost>(
            services,
            container,
            configuration);
        configure(builder);
        builder.Build();
        return services;
    }
}

/// <summary>Configures an Azure Functions generated messaging host.</summary>
/// <typeparam name="TNetwork">The generated messaging network declaration.</typeparam>
/// <typeparam name="TParticipant">The generated participant declaration.</typeparam>
/// <typeparam name="THost">The generated Functions host declaration.</typeparam>
public sealed class MessagingFunctionsCompositionBuilder<TNetwork, TParticipant, THost>
    where TNetwork : class
    where TParticipant : class
    where THost : class
{
    private readonly IServiceCollection _services;
    private readonly Container _container;
    private readonly IConfiguration _configuration;
    private IMessagingDataBus? _dataBus;
    private MessagingFunctionsRuntimeTransport? _transport;
    private StorageQueueFunctionsHostSettings? _storageQueueSettings;
    private bool _messagePack;
    private bool _protobuf;

    internal MessagingFunctionsCompositionBuilder(
        IServiceCollection services,
        Container container,
        IConfiguration configuration)
    {
        _services = services;
        _container = container;
        _configuration = configuration;
    }

    /// <summary>Uses the Azure Service Bus Functions transport.</summary>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder<TNetwork, TParticipant, THost> UseAzureServiceBus()
    {
        _selectTransport(MessagingFunctionsRuntimeTransport.AzureServiceBus);
        return this;
    }

    /// <summary>Uses the Azure Storage Queue Functions transport.</summary>
    /// <param name="settings">Optional effective host settings.</param>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder<TNetwork, TParticipant, THost> UseAzureStorageQueue(
        StorageQueueFunctionsHostSettings? settings = null)
    {
        _selectTransport(MessagingFunctionsRuntimeTransport.AzureStorageQueue);
        _storageQueueSettings = settings;
        return this;
    }

    /// <summary>Uses the supplied DataBus.</summary>
    /// <param name="dataBus">The DataBus.</param>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder<TNetwork, TParticipant, THost> UseDataBus(
        IMessagingDataBus dataBus)
    {
        ArgumentNullException.ThrowIfNull(dataBus);
        if (_dataBus is not null)
            throw new InvalidOperationException("A messaging DataBus is already selected.");
        _dataBus = dataBus;
        return this;
    }

    /// <summary>Uses an in-memory DataBus.</summary>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder<TNetwork, TParticipant, THost> UseInMemoryDataBus()
    {
        return UseDataBus(new InMemoryMessagingDataBus());
    }

    /// <summary>Enables the MessagePack codec.</summary>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder<TNetwork, TParticipant, THost> UseMessagePack()
    {
        _messagePack = true;
        return this;
    }

    /// <summary>Enables the protobuf codec.</summary>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder<TNetwork, TParticipant, THost> UseProtobuf()
    {
        _protobuf = true;
        return this;
    }

    /// <summary>Rejects an invalid Functions outbox processor selection.</summary>
    /// <param name="contextFactory">The unsupported processor context factory.</param>
    /// <param name="batchSize">The unsupported batch size.</param>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder<TNetwork, TParticipant, THost> UseOutbox(
        Ark.Tools.Outbox.IOutboxAsyncContextFactory contextFactory,
        int batchSize = 10)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        throw new InvalidOperationException(
            "Azure Functions cannot host the native messaging outbox processor.");
    }

    /// <summary>Completes the Functions composition.</summary>
    public void Build()
    {
        if (!_transport.HasValue)
            throw new InvalidOperationException(
                "Functions messaging composition requires a transport.");
        var dataBus = _dataBus
            ?? throw new InvalidOperationException(
                "Functions messaging composition requires a DataBus.");
        var manifest = typeof(THost).GetProperty(
                "Manifest",
                BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null) as MessagingFunctionsManifest
            ?? throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Generated Functions host '{0}' does not provide Manifest.",
                    typeof(THost)));

        if (_messagePack)
            _services.AddMessagePackMessagingCodec();
        if (_protobuf)
            _services.AddProtobufMessagingCodec();
        _services.AddArkMessagingFunctionsHost(
            _container,
            _configuration,
            manifest,
            dataBus,
            _transport.Value,
            _storageQueueSettings);
    }

    private void _selectTransport(MessagingFunctionsRuntimeTransport transport)
    {
        if (_transport.HasValue)
            throw new InvalidOperationException("A Functions transport is already selected.");
        _transport = transport;
    }
}
