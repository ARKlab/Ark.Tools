// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Rebus;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.WebInterface;

internal sealed class SampleBusHostedService : IHostedService
{
    private readonly Container _container;

    public SampleBusHostedService(Container container)
    {
        _container = container;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _container.Verify();
        _container.StartBus();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }
}
