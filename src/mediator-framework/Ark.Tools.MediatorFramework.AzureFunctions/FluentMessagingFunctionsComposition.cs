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
    /// <summary>Configures one generated Azure Functions messaging host from generated metadata.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="container">The application Simple Injector container.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="manifest">The generated Functions host manifest.</param>
    /// <param name="configure">The Functions composition callback.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection ConfigureArkMessagingFunctions(
        this IServiceCollection services,
        Container container,
        IConfiguration configuration,
        MessagingFunctionsManifest manifest,
        Action<MessagingFunctionsCompositionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new MessagingFunctionsCompositionBuilder(
            services,
            container,
            configuration,
            manifest);
        configure(builder);
        builder.Build();
        return services;
    }

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

/// <summary>Configures an Azure Functions generated messaging host from a manifest.</summary>
public sealed class MessagingFunctionsCompositionBuilder
{
    private readonly MessagingFunctionsCompositionBuilder<
        MessagingCompositionNetwork,
        MessagingCompositionNetwork,
        MessagingCompositionHost> _inner;

    internal MessagingFunctionsCompositionBuilder(
        IServiceCollection services,
        Container container,
        IConfiguration configuration,
        MessagingFunctionsManifest manifest)
    {
        _inner = new MessagingFunctionsCompositionBuilder<
            MessagingCompositionNetwork,
            MessagingCompositionNetwork,
            MessagingCompositionHost>(
            services,
            container,
            configuration,
            manifest);
    }

    /// <summary>Uses the Azure Service Bus Functions transport.</summary>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder UseAzureServiceBus()
    {
        _inner.UseAzureServiceBus();
        return this;
    }

    /// <summary>Uses the Azure Storage Queue Functions transport.</summary>
    /// <param name="settings">Optional effective host settings.</param>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder UseAzureStorageQueue(
        StorageQueueFunctionsHostSettings? settings = null)
    {
        _inner.UseAzureStorageQueue(settings);
        return this;
    }

    /// <summary>Uses the supplied DataBus.</summary>
    /// <param name="dataBus">The DataBus.</param>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder UseDataBus(IMessagingDataBus dataBus)
    {
        _inner.UseDataBus(dataBus);
        return this;
    }

    /// <summary>Uses an in-memory DataBus.</summary>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder UseInMemoryDataBus()
    {
        _inner.UseInMemoryDataBus();
        return this;
    }

    /// <summary>Enables the MessagePack codec.</summary>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder UseMessagePack()
    {
        _inner.UseMessagePack();
        return this;
    }

    /// <summary>Enables the protobuf codec.</summary>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder UseProtobuf()
    {
        _inner.UseProtobuf();
        return this;
    }

    /// <summary>Selects the generated resource lifecycle policy.</summary>
    /// <param name="lifecycle">The lifecycle policy.</param>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder UseResourceLifecycle(
        MessagingResourceLifecycle lifecycle)
    {
        _inner.UseResourceLifecycle(lifecycle);
        return this;
    }

    /// <summary>Enables transactional outbox enlistment without hosting a processor.</summary>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder UseOutbox()
    {
        _inner.UseOutbox();
        return this;
    }

    /// <summary>Rejects selection of a hosted native outbox processor.</summary>
    /// <param name="contextFactory">The unsupported processor context factory.</param>
    /// <param name="batchSize">The unsupported batch size.</param>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder UseOutbox(
        Ark.Tools.Outbox.IOutboxAsyncContextFactory contextFactory,
        int batchSize = 10)
    {
        _inner.UseOutbox(contextFactory, batchSize);
        return this;
    }

    /// <summary>Completes the Functions composition.</summary>
    public void Build()
    {
        _inner.Build();
    }
}

/// <summary>Marker type used by the metadata-based Functions fluent entry point.</summary>
public sealed class MessagingCompositionHost;

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
    private MessagingResourceLifecycle? _lifecycle;
    private bool _outboxEnqueue;
    private readonly MessagingFunctionsManifest? _manifest;
    private readonly bool _metadataManifest;

    internal MessagingFunctionsCompositionBuilder(
        IServiceCollection services,
        Container container,
        IConfiguration configuration)
    {
        _services = services;
        _container = container;
        _configuration = configuration;
    }

    internal MessagingFunctionsCompositionBuilder(
        IServiceCollection services,
        Container container,
        IConfiguration configuration,
        MessagingFunctionsManifest manifest)
        : this(services, container, configuration)
    {
        _manifest = manifest;
        _metadataManifest = true;
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
        if (_messagePack)
            throw new InvalidOperationException("The MessagePack codec is already selected.");
        _messagePack = true;
        return this;
    }

    /// <summary>Enables the protobuf codec.</summary>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder<TNetwork, TParticipant, THost> UseProtobuf()
    {
        if (_protobuf)
            throw new InvalidOperationException("The protobuf codec is already selected.");
        _protobuf = true;
        return this;
    }

    /// <summary>Selects the generated network resource lifecycle policy.</summary>
    /// <param name="lifecycle">The lifecycle policy.</param>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder<TNetwork, TParticipant, THost> UseResourceLifecycle(
        MessagingResourceLifecycle lifecycle)
    {
        if (_lifecycle.HasValue)
            throw new InvalidOperationException("A resource lifecycle is already selected.");
        _lifecycle = lifecycle;
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
        throw new InvalidOperationException(
            "Azure Functions cannot host the native messaging outbox processor.");
    }

    /// <summary>Enables transactional outbox enlistment without hosting a processor.</summary>
    /// <returns>This builder.</returns>
    public MessagingFunctionsCompositionBuilder<TNetwork, TParticipant, THost> UseOutbox()
    {
        if (_outboxEnqueue)
            throw new InvalidOperationException("A messaging outbox is already selected.");
        _outboxEnqueue = true;
        return this;
    }

    /// <summary>Completes the Functions composition.</summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2090",
        Justification = "Generated Functions declarations are preserved by the consuming source generator.")]
    public void Build()
    {
        if (!_transport.HasValue)
            throw new InvalidOperationException(
                "Functions messaging composition requires a transport.");
        var dataBus = _dataBus
            ?? throw new InvalidOperationException(
                "Functions messaging composition requires a DataBus.");
        var manifest = _manifest
            ?? typeof(THost).GetProperty(
                    "Manifest",
                    BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null) as MessagingFunctionsManifest
            ?? throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Generated Functions host '{0}' does not provide Manifest.",
                    typeof(THost)));
        if (!_metadataManifest && manifest.Participant != typeof(TParticipant))
            throw new InvalidOperationException(
                "The fluent participant does not match the generated Functions host manifest.");
        if (!_metadataManifest && manifest.Network != typeof(TNetwork))
            throw new InvalidOperationException(
                "The fluent network does not match the generated Functions host manifest.");
        if (_lifecycle.HasValue && manifest.Descriptor is not null
            && _lifecycle.Value != manifest.Descriptor.Network.ResourceLifecycle)
            throw new InvalidOperationException(
                "The selected resource lifecycle does not match the generated network declaration.");

        if (_messagePack)
            _services._addMessagePackMessagingCodec();
        if (_protobuf)
            _services._addProtobufMessagingCodec();
        if (_outboxEnqueue)
            _services._addArkMessagingOutboxEnqueue();
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
