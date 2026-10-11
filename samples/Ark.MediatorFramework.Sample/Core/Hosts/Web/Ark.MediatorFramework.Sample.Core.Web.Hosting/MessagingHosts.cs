// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.Tools.MediatorFramework;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Core.Web.Hosting;

/// <summary>What one isolated messaging host needs.</summary>
/// <remarks>A class rather than a record, so no generated <c>ToString</c> prints the application options.</remarks>
public sealed class MessagingHostOptions
{
    /// <summary>Gets the application options of the host's own container.</summary>
    public required ApplicationOptions Application { get; init; }

    /// <summary>Gets the factory of the host's own transport, which the host disposes when it stops.</summary>
    public required Func<IMessagingTransport> Transport { get; init; }

    /// <summary>Gets the claim-check DataBus.</summary>
    public required IMessagingDataBus DataBus { get; init; }

    /// <summary>
    /// Gets the resource manager that provisions the participant's queue, subscriptions and topics;
    /// <see langword="null"/> when the transport manages its own, as the in-memory transport does.
    /// </summary>
    public IMessagingTransportManagement? ResourceManagement { get; init; }
}

/// <summary>Hosts one receiving participant with its own application container, transport and processor.</summary>
/// <typeparam name="TParticipant">The receiving participant.</typeparam>
/// <param name="loggerFactory">The process logger factory.</param>
/// <param name="options">The host options.</param>
/// <param name="registerApplication">Adds participant-specific registrations to the application container.</param>
public sealed class ReceiverHost<TParticipant>(
    ILoggerFactory loggerFactory,
    MessagingHostOptions options,
    Action<Container>? registerApplication) : IsolatedMessagingHost(loggerFactory)
    where TParticipant : class, IMessagingParticipant<TParticipant>
{
    private Container? _container;
    private IMessagingTransport? _transport;

    /// <inheritdoc />
    protected override void Compose(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(options);
        _container = WebHosting.CreateContainer(options.Application);
        registerApplication?.Invoke(_container);
        _transport = options.Transport();
        WebHosting.AddParticipant<TParticipant>(
            services, _container, _transport, options.DataBus, options.ResourceManagement, receiver: true);
        WebHosting.BridgeBus(_container, () => Services);
    }

    /// <inheritdoc />
    protected override void OnBuilt(IServiceProvider services)
    {
        _container!.Verify();
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeCompositionAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync().ConfigureAwait(false);
        if (_transport is IAsyncDisposable transport)
            await transport.DisposeAsync().ConfigureAwait(false);
        _container = null;
        _transport = null;
    }
}

/// <summary>Hosts the outbox processor, which dispatches the committed envelopes of every participant.</summary>
/// <param name="loggerFactory">The process logger factory.</param>
/// <param name="transport">The factory of the host's own transport, which the host disposes when it stops.</param>
/// <param name="contextFactory">The outbox context factory.</param>
/// <param name="batchSize">The maximum number of envelopes dispatched per poll.</param>
public sealed class OutboxProcessorHost(
    ILoggerFactory loggerFactory,
    Func<IMessagingTransport> transport,
    IOutboxAsyncContextFactory contextFactory,
    int batchSize = 10) : IsolatedMessagingHost(loggerFactory)
{
    private IMessagingTransport? _transport;

    /// <inheritdoc />
    protected override void Compose(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport();
        services.AddSingleton(_transport);
        services.AddArkMessagingOutboxProcessor(contextFactory, batchSize);
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeCompositionAsync()
    {
        if (_transport is IAsyncDisposable owned)
            await owned.DisposeAsync().ConfigureAwait(false);
        _transport = null;
    }
}

/// <summary>Registers isolated messaging hosts in a process.</summary>
public static class MessagingHostsServiceCollectionExtensions
{
    /// <summary>Adds a receiving participant as one isolated hosted service.</summary>
    /// <typeparam name="TParticipant">The receiving participant.</typeparam>
    /// <param name="services">The process service collection.</param>
    /// <param name="options">Creates the host options from the process provider.</param>
    /// <param name="registerApplication">Adds participant-specific registrations to the host's application container.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddReceiverHost<TParticipant>(
        this IServiceCollection services,
        Func<IServiceProvider, MessagingHostOptions> options,
        Action<Container>? registerApplication = null)
        where TParticipant : class, IMessagingParticipant<TParticipant>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton<IHostedService>(serviceProvider => new ReceiverHost<TParticipant>(
            serviceProvider.GetRequiredService<ILoggerFactory>(),
            options(serviceProvider),
            registerApplication));
        return services;
    }

    /// <summary>Adds the outbox processor as one isolated hosted service.</summary>
    /// <param name="services">The process service collection.</param>
    /// <param name="transport">Creates the host's own transport from the process provider.</param>
    /// <param name="contextFactory">Creates the outbox context factory from the process provider.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddOutboxProcessorHost(
        this IServiceCollection services,
        Func<IServiceProvider, IMessagingTransport> transport,
        Func<IServiceProvider, IOutboxAsyncContextFactory> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(contextFactory);
        services.AddSingleton<IHostedService>(serviceProvider => new OutboxProcessorHost(
            serviceProvider.GetRequiredService<ILoggerFactory>(),
            () => transport(serviceProvider),
            contextFactory(serviceProvider)));
        return services;
    }
}
