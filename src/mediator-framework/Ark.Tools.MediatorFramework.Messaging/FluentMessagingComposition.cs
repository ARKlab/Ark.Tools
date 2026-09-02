// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NodaTime;

using SimpleInjector;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Provides the single entry point for native messaging composition.</summary>
public static class FluentMessagingCompositionExtensions
{
    /// <summary>Configures one generated messaging network.</summary>
    /// <typeparam name="TNetwork">The generated messaging network declaration.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The composition callback.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection ConfigureArkMessaging<TNetwork>(
        this IServiceCollection services,
        Action<MessagingCompositionBuilder<TNetwork>> configure)
        where TNetwork : class, IMessagingNetwork<TNetwork>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new MessagingCompositionBuilder<TNetwork>(services);
        configure(builder);
        builder.Build();
        return services;
    }

    /// <summary>Configures one generated messaging network from generated metadata.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="network">The generated network options.</param>
    /// <param name="registry">The generated contract registry.</param>
    /// <param name="configure">The composition callback.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection ConfigureArkMessaging(
        this IServiceCollection services,
        MessagingNetworkOptions network,
        IMessagingContractRegistry registry,
        Action<MessagingCompositionBuilder<MessagingCompositionNetwork>> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new MessagingCompositionBuilder<MessagingCompositionNetwork>(
            services,
            network,
            registry);
        configure(builder);
        builder.Build();
        return services;
    }
}

/// <summary>Marker type used by the metadata-based fluent messaging entry point.</summary>
public sealed class MessagingCompositionNetwork :
    IMessagingNetwork<MessagingCompositionNetwork>,
    IMessagingParticipant<MessagingCompositionNetwork>
{
    static MessagingNetworkOptions IMessagingNetwork<MessagingCompositionNetwork>.CreateOptions() =>
        throw new NotSupportedException();

    static IMessagingContractRegistry IMessagingNetwork<MessagingCompositionNetwork>.Registry =>
        throw new NotSupportedException();

    static MessagingParticipantDescriptor
        IMessagingParticipant<MessagingCompositionNetwork>.CreateDescriptor(
            MessagingNetworkOptions network,
            IMessagingContractRegistry registry) =>
        throw new NotSupportedException();
}

/// <summary>Configures one generated network and exactly one native hosting mode.</summary>
/// <typeparam name="TNetwork">The generated messaging network declaration.</typeparam>
public sealed class MessagingCompositionBuilder<TNetwork>
    where TNetwork : class, IMessagingNetwork<TNetwork>
{
    private readonly IServiceCollection _services;
    private readonly MessagingNetworkOptions _network;
    private readonly IMessagingContractRegistry _registry;
    private bool _modeSelected;
    private Action? _registration;

    internal MessagingCompositionBuilder(IServiceCollection services)
    {
        _services = services;
        (_network, _registry) = _resolveNetwork();
    }

    internal MessagingCompositionBuilder(
        IServiceCollection services,
        MessagingNetworkOptions network,
        IMessagingContractRegistry registry)
    {
        _services = services;
        _network = network;
        _registry = registry;
    }

    /// <summary>Configures a producer-only participant.</summary>
    /// <typeparam name="TParticipant">The generated participant declaration.</typeparam>
    /// <param name="configure">The producer configuration.</param>
    /// <returns>This builder.</returns>
    public MessagingCompositionBuilder<TNetwork> Producer<TParticipant>(
        Action<MessagingProducerBuilder<TNetwork, TParticipant>> configure)
        where TParticipant : class, IMessagingParticipant<TParticipant>
    {
        _selectMode();
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new MessagingProducerBuilder<TNetwork, TParticipant>(
            _services,
            _network,
            _registry);
        configure(builder);
        _registration = builder._register;
        return this;
    }

    /// <summary>Configures a custom-hosted receiving participant.</summary>
    /// <typeparam name="TParticipant">The generated participant declaration.</typeparam>
    /// <param name="container">The application Simple Injector container.</param>
    /// <param name="configure">The receiver configuration.</param>
    /// <returns>This builder.</returns>
    public MessagingCompositionBuilder<TNetwork> Receiver<TParticipant>(
        Container container,
        Action<MessagingReceiverBuilder<TNetwork, TParticipant>> configure)
        where TParticipant : class, IMessagingParticipant<TParticipant>
    {
        _selectMode();
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new MessagingReceiverBuilder<TNetwork, TParticipant>(
            _services,
            _network,
            _registry,
            container);
        configure(builder);
        _registration = builder._register;
        return this;
    }

    /// <summary>Completes the composition and reports missing hosting decisions.</summary>
    public void Build()
    {
        if (!_modeSelected || _registration is null)
            throw new InvalidOperationException(
                "Messaging composition requires exactly one Producer or Receiver hosting mode.");
        _registration();
    }

    private void _selectMode()
    {
        if (_modeSelected)
            throw new InvalidOperationException(
                "Messaging composition can select only one hosting mode.");
        _modeSelected = true;
    }

    private static (MessagingNetworkOptions Network, IMessagingContractRegistry Registry) _resolveNetwork()
    {
        return (TNetwork.CreateOptions(), TNetwork.Registry);
    }

    internal static MessagingParticipantDescriptor _resolveParticipant<TParticipant>(
        MessagingNetworkOptions network,
        IMessagingContractRegistry registry)
        where TParticipant : class, IMessagingParticipant<TParticipant>
    {
        return TParticipant.CreateDescriptor(network, registry);
    }
}

/// <summary>Configures the transport implementation used by a messaging composition.</summary>
public sealed class MessagingTransportBuilder
{
    private readonly Action<IMessagingTransport> _select;

    internal MessagingTransportBuilder(Action<IMessagingTransport> select)
    {
        _select = select;
    }

    /// <summary>Uses an in-memory transport.</summary>
    /// <returns>This builder.</returns>
    public MessagingTransportBuilder UseInMemory()
    {
        _select(new InMemoryMessagingTransport());
        return this;
    }

    /// <summary>Uses Azure Service Bus.</summary>
    /// <param name="client">The configured Service Bus client.</param>
    /// <returns>This builder.</returns>
    public MessagingTransportBuilder UseServiceBus(Azure.Messaging.ServiceBus.ServiceBusClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
#pragma warning disable CA2000 // Ownership is transferred to the composition service provider.
        _select(new ServiceBusMessagingTransport(client));
#pragma warning restore CA2000
        return this;
    }

    /// <summary>Uses Azure Storage Queues.</summary>
    /// <param name="client">The configured Queue Storage service client.</param>
    /// <returns>This builder.</returns>
    public MessagingTransportBuilder UseStorageQueue(Azure.Storage.Queues.QueueServiceClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _select(new StorageQueueMessagingTransport(client));
        return this;
    }

    /// <summary>Uses a supplied transport.</summary>
    /// <param name="transport">The transport.</param>
    /// <returns>This builder.</returns>
    public MessagingTransportBuilder Use(IMessagingTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _select(transport);
        return this;
    }
}

/// <summary>Configures the DataBus implementation used by a messaging composition.</summary>
public sealed class MessagingDataBusBuilder
{
    private readonly Action<IMessagingDataBus> _select;

    internal MessagingDataBusBuilder(Action<IMessagingDataBus> select)
    {
        _select = select;
    }

    /// <summary>Uses an in-memory DataBus.</summary>
    /// <param name="clock">The optional clock.</param>
    /// <param name="lifetime">The optional attachment lifetime.</param>
    /// <returns>This builder.</returns>
    public MessagingDataBusBuilder UseInMemory(IClock? clock = null, Duration? lifetime = null)
    {
        _select(new InMemoryMessagingDataBus(
            clock ?? SystemClock.Instance,
            lifetime ?? Duration.FromHours(1)));
        return this;
    }

    /// <summary>Uses Azure Blob Storage.</summary>
    /// <param name="options">The Azure Blob DataBus options.</param>
    /// <returns>This builder.</returns>
    public MessagingDataBusBuilder UseAzureBlob(AzureBlobDataBusOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _select(new AzureBlobMessagingDataBus(options));
        return this;
    }

    /// <summary>Uses a supplied DataBus.</summary>
    /// <param name="dataBus">The DataBus.</param>
    /// <returns>This builder.</returns>
    public MessagingDataBusBuilder Use(IMessagingDataBus dataBus)
    {
        ArgumentNullException.ThrowIfNull(dataBus);
        _select(dataBus);
        return this;
    }
}

/// <summary>Configures serialization implementations used by a messaging composition.</summary>
public sealed class MessagingSerializationBuilder
{
    private readonly Action<bool, bool> _select;

    internal MessagingSerializationBuilder(Action<bool, bool> select)
    {
        _select = select;
    }

    /// <summary>Uses the JSON codec.</summary>
    /// <returns>This builder.</returns>
    public MessagingSerializationBuilder UseJson()
    {
        _select(false, false);
        return this;
    }

    /// <summary>Uses the MessagePack codec.</summary>
    /// <returns>This builder.</returns>
    public MessagingSerializationBuilder UseMessagePack()
    {
        _select(true, false);
        return this;
    }

    /// <summary>Uses the protobuf codec.</summary>
    /// <returns>This builder.</returns>
    public MessagingSerializationBuilder UseProtobuf()
    {
        _select(false, true);
        return this;
    }
}

/// <summary>Configures outbox behavior for a messaging composition.</summary>
public sealed class MessagingOutboxBuilder
{
    private readonly Action _enqueue;
    private readonly Action<IOutboxAsyncContextFactory, int> _processor;

    internal MessagingOutboxBuilder(
        Action enqueue,
        Action<IOutboxAsyncContextFactory, int> processor)
    {
        _enqueue = enqueue;
        _processor = processor;
    }

    /// <summary>Enables outbox enlistment without hosting a processor.</summary>
    /// <returns>This builder.</returns>
    public MessagingOutboxBuilder UseEnqueue()
    {
        _enqueue();
        return this;
    }

    /// <summary>Enables outbox enqueue and processing.</summary>
    /// <param name="contextFactory">The durable outbox context factory.</param>
    /// <param name="batchSize">The maximum batch size.</param>
    /// <returns>This builder.</returns>
    public MessagingOutboxBuilder UseProcessor(
        IOutboxAsyncContextFactory contextFactory,
        int batchSize = 10)
    {
        _processor(contextFactory, batchSize);
        return this;
    }
}

/// <summary>Configures shared producer and receiver messaging decisions.</summary>
/// <typeparam name="TNetwork">The generated messaging network declaration.</typeparam>
/// <typeparam name="TParticipant">The generated participant declaration.</typeparam>
public abstract class MessagingModeBuilder<TNetwork, TParticipant>
    where TNetwork : class, IMessagingNetwork<TNetwork>
    where TParticipant : class, IMessagingParticipant<TParticipant>
{
    private readonly IServiceCollection _services;
    private readonly MessagingNetworkOptions _network;
    private readonly IMessagingContractRegistry _registry;
    private IMessagingTransport? _transport;
    private IMessagingDataBus? _dataBus;
    private IReadOnlyList<Type> _outgoingSteps = Array.Empty<Type>();
    private IReadOnlyList<Type> _incomingSteps = Array.Empty<Type>();
    private bool _messagePack;
    private bool _protobuf;
    private MessagingResourceLifecycle? _lifecycle;
    private IOutboxAsyncContextFactory? _outboxFactory;
    private int _outboxBatchSize = 10;
    private bool _outboxEnqueue;
    private bool _outgoingPipelineSelected;
    private bool _incomingPipelineSelected;

    protected MessagingModeBuilder(
        IServiceCollection services,
        MessagingNetworkOptions network,
        IMessagingContractRegistry registry)
    {
        _services = services;
        _network = network;
        _registry = registry;
    }

    /// <summary>Uses the supplied transport.</summary>
    /// <param name="transport">The transport.</param>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseTransport(IMessagingTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = _transport is null
            ? transport
            : throw new InvalidOperationException("A messaging transport is already selected.");
        return this;
    }

    /// <summary>Configures the transport implementation.</summary>
    /// <param name="configure">The transport configuration callback.</param>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseTransport(
        Action<MessagingTransportBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(new MessagingTransportBuilder(transport => UseTransport(transport)));
        return this;
    }

    /// <summary>Uses the in-memory transport.</summary>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseInMemoryTransport()
    {
        return UseTransport(new InMemoryMessagingTransport());
    }

    /// <summary>Uses the supplied DataBus.</summary>
    /// <param name="dataBus">The DataBus.</param>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseDataBus(IMessagingDataBus dataBus)
    {
        ArgumentNullException.ThrowIfNull(dataBus);
        _dataBus = _dataBus is null
            ? dataBus
            : throw new InvalidOperationException("A messaging DataBus is already selected.");
        return this;
    }

    /// <summary>Configures the DataBus implementation.</summary>
    /// <param name="configure">The DataBus configuration callback.</param>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseDataBus(
        Action<MessagingDataBusBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(new MessagingDataBusBuilder(dataBus => UseDataBus(dataBus)));
        return this;
    }

    /// <summary>Uses an in-memory DataBus.</summary>
    /// <param name="clock">The optional clock.</param>
    /// <param name="lifetime">The optional attachment lifetime.</param>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseInMemoryDataBus(
        IClock? clock = null,
        Duration? lifetime = null)
    {
        return UseDataBus(new InMemoryMessagingDataBus(
            clock ?? SystemClock.Instance,
            lifetime ?? Duration.FromHours(1)));
    }

    /// <summary>Uses an Azure Blob DataBus.</summary>
    /// <param name="options">The Azure Blob DataBus options.</param>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseAzureBlobDataBus(
        AzureBlobDataBusOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_dataBus is not null)
            throw new InvalidOperationException("A messaging DataBus is already selected.");
        _dataBus = new AzureBlobMessagingDataBus(options);
        return this;
    }

    /// <summary>Enables the MessagePack codec.</summary>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseMessagePack()
    {
        if (_messagePack)
            throw new InvalidOperationException("The MessagePack codec is already selected.");
        _messagePack = true;
        return this;
    }

    /// <summary>Configures the serialization implementations.</summary>
    /// <param name="configure">The serialization configuration callback.</param>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseSerialization(
        Action<MessagingSerializationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(new MessagingSerializationBuilder((messagePack, protobuf) =>
        {
            if (messagePack)
                UseMessagePack();
            if (protobuf)
                UseProtobuf();
        }));
        return this;
    }

    /// <summary>Enables the protobuf codec.</summary>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseProtobuf()
    {
        if (_protobuf)
            throw new InvalidOperationException("The protobuf codec is already selected.");
        _protobuf = true;
        return this;
    }

    /// <summary>Uses the supplied outgoing pipeline steps.</summary>
    /// <param name="stepTypes">The step types in execution order.</param>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseOutgoingPipeline(
        params Type[] stepTypes)
    {
        ArgumentNullException.ThrowIfNull(stepTypes);
        if (stepTypes.Length == 0)
            throw new ArgumentException("Pipeline must contain at least one step.", nameof(stepTypes));
        if (stepTypes.Any(static type => type is null))
            throw new ArgumentException("Pipeline step types cannot contain null.", nameof(stepTypes));
        if (_outgoingPipelineSelected)
            throw new InvalidOperationException("An outgoing pipeline is already selected.");
        _outgoingSteps = stepTypes.ToArray();
        _outgoingPipelineSelected = true;
        return this;
    }

    /// <summary>Uses the supplied incoming pipeline steps.</summary>
    /// <param name="stepTypes">The step types in execution order.</param>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseIncomingPipeline(
        params Type[] stepTypes)
    {
        ArgumentNullException.ThrowIfNull(stepTypes);
        if (stepTypes.Length == 0)
            throw new ArgumentException("Pipeline must contain at least one step.", nameof(stepTypes));
        if (stepTypes.Any(static type => type is null))
            throw new ArgumentException("Pipeline step types cannot contain null.", nameof(stepTypes));
        if (_incomingPipelineSelected)
            throw new InvalidOperationException("An incoming pipeline is already selected.");
        _incomingSteps = stepTypes.ToArray();
        _incomingPipelineSelected = true;
        return this;
    }

    /// <summary>Selects the resource lifecycle policy.</summary>
    /// <param name="lifecycle">The lifecycle policy.</param>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseResourceLifecycle(
        MessagingResourceLifecycle lifecycle)
    {
        if (_lifecycle.HasValue)
            throw new InvalidOperationException("A resource lifecycle is already selected.");
        _lifecycle = lifecycle;
        return this;
    }

    /// <summary>Enables outbox enlistment without hosting a processor.</summary>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseOutbox()
    {
        if (_outboxFactory is not null || _outboxEnqueue)
            throw new InvalidOperationException("A messaging outbox is already selected.");
        _outboxEnqueue = true;
        return this;
    }

    /// <summary>Enables native outbox enqueue and processing.</summary>
    /// <param name="contextFactory">The durable outbox context factory.</param>
    /// <param name="batchSize">The maximum batch size.</param>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseOutbox(
        IOutboxAsyncContextFactory contextFactory,
        int batchSize = 10)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        if (_outboxFactory is not null || _outboxEnqueue)
            throw new InvalidOperationException("A messaging outbox is already selected.");
        _outboxFactory = contextFactory;
        _outboxBatchSize = batchSize;
        return this;
    }

    /// <summary>Configures outbox behavior.</summary>
    /// <param name="configure">The outbox configuration callback.</param>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseOutbox(
        Action<MessagingOutboxBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(new MessagingOutboxBuilder(
            () => UseOutbox(),
            (factory, batchSize) => UseOutbox(factory, batchSize)));
        return this;
    }

    internal void _registerCommon(MessagingParticipantDescriptor participant)
    {
        var transport = _transport
            ?? throw new InvalidOperationException("Messaging composition requires a transport.");
        var dataBus = _dataBus
            ?? throw new InvalidOperationException("Messaging composition requires a DataBus.");
        if (_lifecycle.HasValue && _lifecycle.Value != participant.Network.ResourceLifecycle)
            throw new InvalidOperationException(
                "The selected resource lifecycle does not match the generated network declaration.");

        _services._addArkMessagingParticipant(
            participant,
            transport,
            dataBus,
            _outgoingSteps);
        if (_messagePack)
            _services._addMessagePackMessagingCodec();
        if (_protobuf)
            _services._addProtobufMessagingCodec();
        if (_outboxFactory is not null)
            _services.AddArkMessagingOutboxProcessor(_outboxFactory, _outboxBatchSize);
        else if (_outboxEnqueue)
            _services._addArkMessagingOutboxEnqueue();
    }

    internal IMessagingTransport _transportValue =>
        _transport ?? throw new InvalidOperationException("Messaging composition requires a transport.");

    internal MessagingNetworkOptions _networkOptions => _network;

    internal IMessagingContractRegistry _registryValue => _registry;

    internal IServiceCollection _servicesValue => _services;

    protected IReadOnlyList<Type> IncomingSteps => _incomingSteps;
}

/// <summary>Configures a producer-only messaging participant.</summary>
/// <typeparam name="TNetwork">The generated messaging network declaration.</typeparam>
/// <typeparam name="TParticipant">The generated participant declaration.</typeparam>
public sealed class MessagingProducerBuilder<TNetwork, TParticipant>
    : MessagingModeBuilder<TNetwork, TParticipant>
    where TNetwork : class, IMessagingNetwork<TNetwork>
    where TParticipant : class, IMessagingParticipant<TParticipant>
{
    internal MessagingProducerBuilder(
        IServiceCollection services,
        MessagingNetworkOptions network,
        IMessagingContractRegistry registry)
        : base(services, network, registry)
    {
    }

    /// <summary>Registers the configured producer participant.</summary>
    internal void _register()
    {
        var participant = MessagingCompositionBuilder<TNetwork>._resolveParticipant<TParticipant>(
            _networkOptions,
            _registryValue);
        _registerCommon(participant);
    }
}

/// <summary>Configures a custom messaging receiver.</summary>
/// <typeparam name="TNetwork">The generated messaging network declaration.</typeparam>
/// <typeparam name="TParticipant">The generated participant declaration.</typeparam>
public sealed class MessagingReceiverBuilder<TNetwork, TParticipant>
    : MessagingModeBuilder<TNetwork, TParticipant>
    where TNetwork : class, IMessagingNetwork<TNetwork>
    where TParticipant : class, IMessagingParticipant<TParticipant>
{
    private readonly Container _container;

    internal MessagingReceiverBuilder(
        IServiceCollection services,
        MessagingNetworkOptions network,
        IMessagingContractRegistry registry,
        Container container)
        : base(services, network, registry)
    {
        _container = container;
    }

    /// <summary>Registers the configured receiver participant and dispatcher.</summary>
    internal void _register()
    {
        var participant = MessagingCompositionBuilder<TNetwork>._resolveParticipant<TParticipant>(
            _networkOptions,
            _registryValue);
        if (!participant.Receives)
            throw new InvalidOperationException(
                "The selected participant is producer-only and cannot host a receiver.");
        if (_transportValue is not IMessagingReceiveTransport)
            throw new InvalidOperationException(
                "A custom receiver requires a receive-capable messaging transport.");
        _registerCommon(participant);
        _servicesValue.AddSingleton(serviceProvider => new MessagingHeaderProcessor(
            serviceProvider.GetRequiredService<IMessagingCodecRegistry>(),
            participant.Network.NetworkIdentity));
        _servicesValue.AddSingleton(serviceProvider => new MessagingPayloadReceiver(
            serviceProvider.GetRequiredService<IMessagingDataBus>(),
            participant.Network));
        _servicesValue.AddSingleton(serviceProvider => new MessagingDispatcher(
            _container,
            serviceProvider.GetRequiredService<MessagingHeaderProcessor>(),
            serviceProvider.GetRequiredService<MessagingPayloadReceiver>(),
            participant.RetryPolicy,
            (logicalName, payload, processor, ctk) =>
                participant.Dispatch!(logicalName, payload, processor, ctk),
            participant.DispatchFailed is null
                ? null
                : (logicalName, payload, deliveryCount, error, processor, ctk) =>
                    participant.DispatchFailed(
                        logicalName,
                        payload,
                        deliveryCount,
                        error,
                        processor,
                        ctk),
            IncomingSteps,
            _container.GetInstance));
        _servicesValue.AddSingleton<IHostedService, MessagingReceiveHostedService>();
    }

}

internal sealed class MessagingReceiveHostedService : IHostedService, IAsyncDisposable
{
    private readonly IMessagingReceiveTransport _transport;
    private readonly MessagingDispatcher _dispatcher;
    private readonly string _queue;
    private MessagingReceivePump? _pump;

    public MessagingReceiveHostedService(
        IMessagingTransport transport,
        MessagingDispatcher dispatcher,
        MessagingParticipantDescriptor participant)
    {
        _transport = transport as IMessagingReceiveTransport
            ?? throw new InvalidOperationException(
                "A custom receiver requires a receive-capable messaging transport.");
        _dispatcher = dispatcher;
        _queue = participant.Identity;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _pump = new MessagingReceivePump(
            _transport,
            _queue,
            _dispatcher.OnDeliveryAsync);
        await _pump.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_pump is not null)
        {
            await _pump.StopAsync().ConfigureAwait(false);
            _pump = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_pump is not null)
        {
            await _pump.DisposeAsync().ConfigureAwait(false);
            _pump = null;
        }
    }
}
