// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Rebus;
using Ark.MediatorFramework.Sample.RebusProcessor;
using Rebus.Transport.InMem;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.WebInterface;

/// <summary>
/// Hosted service that owns the Rebus processor container: it builds it from Microsoft
/// hosting services (no reference to the API SimpleInjector container), then manages its
/// lifecycle independently.
/// </summary>
internal sealed class SampleBusHostedService : IHostedService
{
    private readonly Container _processorContainer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SampleBusHostedService"/> class.
    /// </summary>
    /// <param name="network">The shared in-memory Rebus transport network (from Microsoft DI).</param>
    /// <param name="useSqlStore">Whether the processor should use SQL persistence and the outbox.</param>
    /// <param name="connectionString">Optional SQL Server connection string.</param>
    public SampleBusHostedService(InMemNetwork network, bool useSqlStore, string? connectionString)
    {
        _processorContainer = RebusProcessorComposition.BuildContainer(
            network,
            useSqlStore: useSqlStore,
            connectionString: connectionString,
            registerHandlers: SampleRebusEndpoints.RegisterHandlers);
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _processorContainer.Verify();
        _processorContainer.StartBus();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processorContainer.DisposeAsync().ConfigureAwait(false);
    }
}

