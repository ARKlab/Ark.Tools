// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Rebus;
using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.RebusProcessor;
using Rebus.Transport.InMem;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.WebInterface;

internal sealed class SampleBusHostedService : IHostedService
{
    private readonly Container _container;
    private readonly Container _processorContainer;

    public SampleBusHostedService(Container container)
    {
        _container = container;
        var network = container.GetInstance<InMemNetwork>();
        var sqlConfig = container.GetRegistration<SampleDataContextConfig>()?.GetInstance() as SampleDataContextConfig;
        _processorContainer = RebusProcessorComposition.BuildContainer(
            network,
            useSqlStore: sqlConfig is not null,
            connectionString: sqlConfig?.ConnectionString,
            registerHandlers: SampleRebusEndpoints.RegisterHandlers,
            configureRouting: SampleRebusEndpoints.ConfigureRouting);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _container.Verify();
        _container.StartBus();
        _processorContainer.Verify();
        _processorContainer.StartBus();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processorContainer.DisposeAsync().ConfigureAwait(false);
    }
}
