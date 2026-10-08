// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid.SimpleInjector;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Core.Tests.Hooks;

/// <summary>One messaging participant composed as its own process: container, service provider, hosted services.</summary>
public sealed class ParticipantProcess : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly IReadOnlyList<IHostedService> _hosted;

    private ParticipantProcess(Container container, ServiceProvider services)
    {
        Container = container;
        _services = services;
        _hosted = services.GetServices<IHostedService>().ToArray();
    }

    /// <summary>Gets the participant's application container.</summary>
    public Container Container { get; }

    /// <summary>Gets the participant's Microsoft DI provider.</summary>
    public IServiceProvider Services => _services;

    /// <summary>Composes and starts one participant.</summary>
    /// <param name="container">The application container, already populated by <c>ApplicationComposition</c>.</param>
    /// <param name="configureMessaging">Registers the participant with <c>ConfigureArkMessaging</c>.</param>
    /// <param name="bridgeBus">
    /// <see langword="true"/> to expose the participant bus to the container; <see langword="false"/> for the
    /// dedicated outbox processor, which registers no bus.
    /// </param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The started participant.</returns>
    public static async Task<ParticipantProcess> StartAsync(
        Container container,
        Action<IServiceCollection> configureMessaging,
        bool bridgeBus = true,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(configureMessaging);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddArkSolidProcessors(container);
        configureMessaging(services);
        var provider = services.BuildServiceProvider(validateScopes: true);
        var started = new List<IHostedService>();
        try
        {
            if (bridgeBus)
            {
                container.RegisterSingleton<IBus>(() => provider.GetRequiredService<IBus>());
                container.RegisterSingleton<IBusOutboxEnlistment>(() => provider.GetRequiredService<IBusOutboxEnlistment>());
            }
            container.Verify();
            var process = new ParticipantProcess(container, provider);
            foreach (var hosted in process._hosted)
            {
                await hosted.StartAsync(ctk).ConfigureAwait(false);
                started.Add(hosted);
            }
            return process;
        }
        catch
        {
            // The caller only owns the process once it is returned, so a failed start is rolled back here.
            for (var i = started.Count - 1; i >= 0; i--)
                await started[i].StopAsync(CancellationToken.None).ConfigureAwait(false);
            await provider.DisposeAsync().ConfigureAwait(false);
            await container.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var hosted in _hosted.Reverse())
            await hosted.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _services.DisposeAsync().ConfigureAwait(false);
        await Container.DisposeAsync().ConfigureAwait(false);
    }
}
