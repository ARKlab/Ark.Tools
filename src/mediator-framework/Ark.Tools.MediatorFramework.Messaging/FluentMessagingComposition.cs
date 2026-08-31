// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Reflection;

using Ark.Tools.Outbox;

using Microsoft.Extensions.DependencyInjection;

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
        where TNetwork : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new MessagingCompositionBuilder<TNetwork>(services);
        configure(builder);
        builder.Build();
        return services;
    }
}

/// <summary>Configures one generated network and exactly one native hosting mode.</summary>
/// <typeparam name="TNetwork">The generated messaging network declaration.</typeparam>
public sealed class MessagingCompositionBuilder<TNetwork>
    where TNetwork : class
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

    /// <summary>Configures a producer-only participant.</summary>
    /// <typeparam name="TParticipant">The generated participant declaration.</typeparam>
    /// <param name="configure">The producer configuration.</param>
    /// <returns>This builder.</returns>
    public MessagingCompositionBuilder<TNetwork> Producer<TParticipant>(
        Action<MessagingProducerBuilder<TNetwork, TParticipant>> configure)
        where TParticipant : class
    {
        _selectMode();
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new MessagingProducerBuilder<TNetwork, TParticipant>(
            _services,
            _network,
            _registry);
        configure(builder);
        _registration = builder.Register;
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
        where TParticipant : class
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
        _registration = builder.Register;
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
        var networkType = typeof(TNetwork);
        var options = networkType.GetMethod(
                "CreateOptions",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                Type.EmptyTypes,
                modifiers: null)
            ?.Invoke(null, null) as MessagingNetworkOptions
            ?? throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Generated messaging network '{0}' does not provide CreateOptions().",
                    networkType));
        var registry = networkType.GetProperty(
                "Registry",
                BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null) as IMessagingContractRegistry
            ?? throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Generated messaging network '{0}' does not provide Registry.",
                    networkType));
        return (options, registry);
    }

    internal static MessagingParticipantDescriptor ResolveParticipant<TParticipant>(
        MessagingNetworkOptions network,
        IMessagingContractRegistry registry)
        where TParticipant : class
    {
        var participantType = typeof(TParticipant);
        var descriptor = participantType.GetMethod(
                "CreateDescriptor",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                [typeof(MessagingNetworkOptions), typeof(IMessagingContractRegistry)],
                modifiers: null)
            ?.Invoke(null, [network, registry]) as MessagingParticipantDescriptor
            ?? throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Generated messaging participant '{0}' does not provide CreateDescriptor().",
                    participantType));
        return descriptor;
    }
}

/// <summary>Configures shared producer and receiver messaging decisions.</summary>
/// <typeparam name="TNetwork">The generated messaging network declaration.</typeparam>
/// <typeparam name="TParticipant">The generated participant declaration.</typeparam>
public abstract class MessagingModeBuilder<TNetwork, TParticipant>
    where TNetwork : class
    where TParticipant : class
{
    private readonly IServiceCollection _services;
    private readonly MessagingNetworkOptions _network;
    private readonly IMessagingContractRegistry _registry;
    private IMessagingTransport? _transport;
    private IMessagingDataBus? _dataBus;
    private IReadOnlyList<Type> _outgoingSteps = Array.Empty<Type>();
    private bool _messagePack;
    private bool _protobuf;
    private MessagingResourceLifecycle? _lifecycle;
    private IOutboxAsyncContextFactory? _outboxFactory;
    private int _outboxBatchSize = 10;

    internal MessagingModeBuilder(
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

    /// <summary>Enables the MessagePack codec.</summary>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseMessagePack()
    {
        _messagePack = true;
        return this;
    }

    /// <summary>Enables the protobuf codec.</summary>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseProtobuf()
    {
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
        if (stepTypes.Any(static type => type is null))
            throw new ArgumentException("Pipeline step types cannot contain null.", nameof(stepTypes));
        _outgoingSteps = stepTypes.ToArray();
        return this;
    }

    /// <summary>Selects the resource lifecycle policy.</summary>
    /// <param name="lifecycle">The lifecycle policy.</param>
    /// <returns>This builder.</returns>
    public MessagingModeBuilder<TNetwork, TParticipant> UseResourceLifecycle(
        MessagingResourceLifecycle lifecycle)
    {
        _lifecycle = lifecycle;
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
        if (_outboxFactory is not null)
            throw new InvalidOperationException("A messaging outbox is already selected.");
        _outboxFactory = contextFactory;
        _outboxBatchSize = batchSize;
        return this;
    }

    internal void RegisterCommon(MessagingParticipantDescriptor participant)
    {
        var transport = _transport
            ?? throw new InvalidOperationException("Messaging composition requires a transport.");
        var dataBus = _dataBus
            ?? throw new InvalidOperationException("Messaging composition requires a DataBus.");
        if (_lifecycle.HasValue && _lifecycle.Value != participant.Network.ResourceLifecycle)
            throw new InvalidOperationException(
                "The selected resource lifecycle does not match the generated network declaration.");

        _services.AddArkMessagingParticipant(
            participant,
            transport,
            dataBus,
            _outgoingSteps);
        if (_messagePack)
            _services.AddMessagePackMessagingCodec();
        if (_protobuf)
            _services.AddProtobufMessagingCodec();
        if (_outboxFactory is not null)
            _services.AddArkMessagingOutboxProcessor(_outboxFactory, _outboxBatchSize);
    }

    internal IMessagingTransport Transport =>
        _transport ?? throw new InvalidOperationException("Messaging composition requires a transport.");

    internal MessagingNetworkOptions Network => _network;

    internal IMessagingContractRegistry Registry => _registry;

    internal IServiceCollection Services => _services;
}

/// <summary>Configures a producer-only messaging participant.</summary>
/// <typeparam name="TNetwork">The generated messaging network declaration.</typeparam>
/// <typeparam name="TParticipant">The generated participant declaration.</typeparam>
public sealed class MessagingProducerBuilder<TNetwork, TParticipant>
    : MessagingModeBuilder<TNetwork, TParticipant>
    where TNetwork : class
    where TParticipant : class
{
    internal MessagingProducerBuilder(
        IServiceCollection services,
        MessagingNetworkOptions network,
        IMessagingContractRegistry registry)
        : base(services, network, registry)
    {
    }

    /// <summary>Registers the configured producer participant.</summary>
    internal void Register()
    {
        var participant = MessagingCompositionBuilder<TNetwork>.ResolveParticipant<TParticipant>(
            Network,
            Registry);
        RegisterCommon(participant);
    }
}

/// <summary>Configures a custom messaging receiver.</summary>
/// <typeparam name="TNetwork">The generated messaging network declaration.</typeparam>
/// <typeparam name="TParticipant">The generated participant declaration.</typeparam>
public sealed class MessagingReceiverBuilder<TNetwork, TParticipant>
    : MessagingModeBuilder<TNetwork, TParticipant>
    where TNetwork : class
    where TParticipant : class
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
    internal void Register()
    {
        var participant = MessagingCompositionBuilder<TNetwork>.ResolveParticipant<TParticipant>(
            Network,
            Registry);
        if (!participant.Receives)
            throw new InvalidOperationException(
                "The selected participant is producer-only and cannot host a receiver.");
        RegisterCommon(participant);
        Services.AddSingleton(serviceProvider => new MessagingHeaderProcessor(
            serviceProvider.GetRequiredService<IMessagingCodecRegistry>(),
            participant.Network.NetworkIdentity));
        Services.AddSingleton(serviceProvider => new MessagingPayloadReceiver(
            serviceProvider.GetRequiredService<IMessagingDataBus>(),
            participant.Network));
        Services.AddSingleton(serviceProvider => new MessagingDispatcher(
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
            Array.Empty<Type>(),
            _container.GetInstance));
    }

}
