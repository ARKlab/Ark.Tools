// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Ark.MediatorFramework.Sample.Core.WebRebus.Tests;

/// <summary>One WebRebus process: its container and started bus.</summary>
internal sealed class RebusTestProcess : IAsyncDisposable
{
    private readonly SettablePrincipalProvider? _principal;

    /// <summary>Initializes a new instance of the <see cref="RebusTestProcess"/> class.</summary>
    /// <param name="container">The verified container with a started bus.</param>
    /// <param name="principal">The settable user, registered only in the api process.</param>
    public RebusTestProcess(Container container, SettablePrincipalProvider? principal)
    {
        Container = container;
        _principal = principal;
    }

    /// <summary>Gets the process container.</summary>
    public Container Container { get; }

    /// <summary>Executes a request through the decorated application handler.</summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request.</param>
    /// <returns>The handler response.</returns>
    public async Task<TResponse> DispatchAsync<TRequest, TResponse>(TRequest request)
        where TRequest : IRequest<TResponse>
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(Container);
        return await Container.GetInstance<IRequestHandler<TRequest, TResponse>>()
            .ExecuteAsync(request).ConfigureAwait(false);
    }

    /// <summary>Sends a message through the process bus.</summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="message">The message.</param>
    /// <returns>A task that completes when the message is sent.</returns>
    public async Task SendAsync<T>(T message)
        where T : class
    {
        await Container.GetInstance<Ark.Tools.MediatorFramework.IBus>().Send(message).ConfigureAwait(false);
    }

    /// <summary>Replaces the scopes of the api process user.</summary>
    /// <param name="scopes">The granted scopes.</param>
    public void SetScopes(params string[] scopes)
    {
        (_principal ?? throw new InvalidOperationException("Only the api process has a settable user.")).Set(scopes);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync().ConfigureAwait(false);
    }
}
