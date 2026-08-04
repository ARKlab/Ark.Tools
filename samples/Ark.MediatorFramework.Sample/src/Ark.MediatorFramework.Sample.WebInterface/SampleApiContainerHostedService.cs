// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.WebInterface;

/// <summary>Hosted service that disposes the API container on shutdown.</summary>
internal sealed class SampleApiContainerHostedService : IHostedService
{
    private readonly Container _container;

    /// <summary>Initializes a new instance of the <see cref="SampleApiContainerHostedService"/> class.</summary>
    /// <param name="container">The API SimpleInjector container.</param>
    public SampleApiContainerHostedService(Container container)
    {
        _container = container;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }
}
