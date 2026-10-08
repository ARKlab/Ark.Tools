// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Core.Functions.Tests;

/// <summary>One Functions process composed in the test: service provider, started hosted services, container.</summary>
internal sealed class FunctionsTestProcess : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly IReadOnlyList<IHostedService> _hosted;
    private readonly Container? _container;

    private FunctionsTestProcess(ServiceProvider services, Container? container)
    {
        _services = services;
        _container = container;
        _hosted = services.GetServices<IHostedService>().ToArray();
    }

    /// <summary>Builds the service provider and starts its hosted services.</summary>
    /// <param name="services">The composed service collection.</param>
    /// <param name="container">The application container, disposed with the process; none for the outbox processor.</param>
    /// <returns>The started process.</returns>
    public static async Task<FunctionsTestProcess> StartAsync(IServiceCollection services, Container? container)
    {
        var process = new FunctionsTestProcess(services.BuildServiceProvider(), container);
        foreach (var hosted in process._hosted)
            await hosted.StartAsync(CancellationToken.None).ConfigureAwait(false);
        return process;
    }

    /// <summary>Executes a request through the bridged request processor, as a generated HTTP function does.</summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request.</param>
    /// <returns>The handler response.</returns>
    public async Task<TResponse> DispatchAsync<TRequest, TResponse>(TRequest request)
        where TRequest : IRequest<TResponse>
    {
        return await _services.GetRequiredService<IRequestProcessor>().ExecuteAsync(request).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var hosted in _hosted.Reverse())
            await hosted.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _services.DisposeAsync().ConfigureAwait(false);
        if (_container is not null)
            await _container.DisposeAsync().ConfigureAwait(false);
    }
}
