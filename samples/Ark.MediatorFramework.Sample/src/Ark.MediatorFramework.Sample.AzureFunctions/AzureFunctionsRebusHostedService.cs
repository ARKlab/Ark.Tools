// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Rebus;

using Microsoft.Extensions.Hosting;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.AzureFunctions;

/// <summary>Owns the outbound Rebus client for the Function process.</summary>
internal sealed class AzureFunctionsRebusHostedService : IHostedService
{
    private readonly Container _container;

    public AzureFunctionsRebusHostedService(Container container)
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
