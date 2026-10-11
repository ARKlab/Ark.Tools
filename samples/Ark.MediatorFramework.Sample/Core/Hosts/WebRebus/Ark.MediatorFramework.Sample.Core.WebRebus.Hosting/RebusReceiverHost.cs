// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.Tools.MediatorFramework.Rebus;
using Ark.Tools.Rebus;
using Ark.Tools.Solid;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Rebus.Config;
using Rebus.DataBus;
using Rebus.Transport;

using SimpleInjector;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Core.WebRebus.Hosting;

/// <summary>What one isolated Rebus receiver needs.</summary>
/// <remarks>A class rather than a record, so no generated <c>ToString</c> prints the application options.</remarks>
public sealed class RebusReceiverHostOptions
{
    /// <summary>Gets the application options of the receiver's own container.</summary>
    public required ApplicationOptions Application { get; init; }

    /// <summary>Gets the transport configuration; the second argument is the receiver's input queue.</summary>
    public required Action<StandardConfigurer<ITransport>, string> Transport { get; init; }

    /// <summary>Gets the DataBus storage configuration.</summary>
    public required Action<StandardConfigurer<IDataBusStorage>> DataBus { get; init; }

    /// <summary>Gets additional Rebus configuration, such as the in-memory stores of tests.</summary>
    public Action<RebusConfigurer>? Configure { get; init; }
}

/// <summary>
/// Hosts one Rebus receiver as an isolated hosted service: its own application container and bus, started and
/// subscribed when the process starts and disposed when it stops. One process can host several of them.
/// </summary>
/// <typeparam name="THost">The generated Rebus host of the participant.</typeparam>
/// <param name="options">The receiver options.</param>
/// <param name="registerApplication">Adds participant-specific registrations to the application container.</param>
/// <param name="startOutboxProcessor">Whether this receiver drains the shared outbox table.</param>
public sealed class RebusReceiverHost<THost>(
    RebusReceiverHostOptions options,
    Action<Container>? registerApplication,
    bool startOutboxProcessor) : IHostedService, IAsyncDisposable
    where THost : IArkRebusHost
{
    private Container? _container;

    /// <summary>Gets the receiver's application container, once started.</summary>
    public Container Container => _container ?? throw new InvalidOperationException("The host is not started.");

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var identity = THost.GetRequirements().Identity;
        _container = RebusHosting.CreateContainer(options.Application);
        _container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, RebusPrincipalContextWithFallbackProvider>();
        registerApplication?.Invoke(_container);
        RebusHosting.Configure<THost>(
            _container,
            t => options.Transport(t, identity),
            options.DataBus,
            startOutboxProcessor,
            configureTest: options.Configure);
        _container.Verify();
        _container.StartBus();
        // Starting the bus does not subscribe: this subscribes the receiver to the events it handles.
        await THost.SubscribeAsync(_container.GetInstance<global::Rebus.Bus.IBus>(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Disposing the container stops and disposes its bus.
        if (_container is not null)
            await _container.DisposeAsync().ConfigureAwait(false);
        _container = null;
    }
}

/// <summary>Registers isolated Rebus receivers in a process.</summary>
public static class RebusReceiverHostServiceCollectionExtensions
{
    /// <summary>Creates the options of an isolated Rebus receiver from the process configuration.</summary>
    /// <remarks>Reads <c>ConnectionStrings:Sample</c>, <c>ConnectionStrings:ServiceBus</c> and <c>ConnectionStrings:DataBus</c>.</remarks>
    /// <param name="configuration">The process configuration.</param>
    /// <returns>The receiver options.</returns>
    public static RebusReceiverHostOptions CreateRebusReceiverHostOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var sql = configuration.GetConnectionString("Sample")
            ?? throw new InvalidOperationException("ConnectionStrings:Sample is required.");
        var serviceBus = configuration.GetConnectionString("ServiceBus")
            ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");
        var dataBus = configuration.GetConnectionString("DataBus")
            ?? throw new InvalidOperationException("ConnectionStrings:DataBus is required.");
        return new RebusReceiverHostOptions
        {
            Application = new ApplicationOptions { SqlConnectionString = sql },
            Transport = (transport, queue) => transport.UseAzureServiceBus(serviceBus, queue),
            DataBus = storage => storage.StoreInBlobStorage(dataBus, RebusHosting.DataBusContainerName),
        };
    }

    /// <summary>Adds a Rebus receiver as one isolated hosted service.</summary>
    /// <typeparam name="THost">The generated Rebus host of the participant.</typeparam>
    /// <param name="services">The process service collection.</param>
    /// <param name="options">Creates the receiver options from the process provider.</param>
    /// <param name="registerApplication">Adds participant-specific registrations to the receiver's container.</param>
    /// <param name="startOutboxProcessor">Whether this receiver drains the shared outbox table; exactly one should.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRebusReceiverHost<THost>(
        this IServiceCollection services,
        Func<IServiceProvider, RebusReceiverHostOptions> options,
        Action<Container>? registerApplication = null,
        bool startOutboxProcessor = false)
        where THost : IArkRebusHost
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton<IHostedService>(serviceProvider => new RebusReceiverHost<THost>(
            options(serviceProvider), registerApplication, startOutboxProcessor));
        return services;
    }
}
