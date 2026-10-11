// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ark.MediatorFramework.Sample.Core.Web.Hosting;

/// <summary>
/// Runs one messaging background host in its own nested composition: a Microsoft DI provider, and the application
/// container and transport it creates. The process sees a single <see cref="IHostedService"/>, so one process can
/// host several of them side by side, for example every processor inside the WebInterface of one App Service.
/// </summary>
/// <remarks>
/// The nested provider shares only the process logger factory. It starts its own hosted services in registration
/// order and stops them in reverse order.
/// </remarks>
public abstract class IsolatedMessagingHost : IHostedService, IAsyncDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private ServiceProvider? _provider;
    private IHostedService[] _hosted = [];

    /// <summary>Initializes a new instance of the <see cref="IsolatedMessagingHost"/> class.</summary>
    /// <param name="loggerFactory">The process logger factory, shared with the nested composition.</param>
    protected IsolatedMessagingHost(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>Gets the nested provider, once started.</summary>
    public IServiceProvider Services => _provider ?? throw new InvalidOperationException("The host is not started.");

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Compose(services);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        OnBuilt(_provider);
        _hosted = _provider.GetServices<IHostedService>().ToArray();
        foreach (var hosted in _hosted)
            await hosted.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var hosted in _hosted.Reverse())
            await hosted.StopAsync(cancellationToken).ConfigureAwait(false);
        _hosted = [];
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
            await _provider.DisposeAsync().ConfigureAwait(false);
        _provider = null;
        await DisposeCompositionAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>Registers the nested composition.</summary>
    /// <param name="services">The nested service collection.</param>
    protected abstract void Compose(IServiceCollection services);

    /// <summary>Runs after the nested provider is built and before its hosted services start.</summary>
    /// <param name="services">The nested provider.</param>
    protected virtual void OnBuilt(IServiceProvider services)
    {
    }

    /// <summary>Disposes what <see cref="Compose"/> created outside the nested provider.</summary>
    /// <returns>A task that completes when the resources are disposed.</returns>
    protected virtual ValueTask DisposeCompositionAsync()
    {
        return ValueTask.CompletedTask;
    }
}
