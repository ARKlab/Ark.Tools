// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Api;

/// <summary>Singleton counter proving the cross-cutting decorator runs on every transport.</summary>
public sealed class AuditCounter
{
    private int _count;

    /// <summary>Gets the number of handled requests.</summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>Records one handled request.</summary>
    public void Increment() => Interlocked.Increment(ref _count);
}

/// <summary>
/// SimpleInjector decorator applied to every <see cref="IRequestHandler{TRequest, TResponse}"/>.
/// Because both the Minimal API endpoint and the Rebus wrapper resolve the decorated handler,
/// this cross-cutting concern applies transport-agnostically.
/// </summary>
public sealed class AuditRequestDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRequestHandler<TRequest, TResponse> _inner;
    private readonly AuditCounter _counter;

    /// <summary>Initializes a new instance of the <see cref="AuditRequestDecorator{TRequest, TResponse}"/> class.</summary>
    public AuditRequestDecorator(IRequestHandler<TRequest, TResponse> inner, AuditCounter counter)
    {
        _inner = inner;
        _counter = counter;
    }

    /// <inheritdoc />
    public async Task<TResponse> ExecuteAsync(TRequest Request, CancellationToken ctk = default)
    {
        _counter.Increment();
        return await _inner.ExecuteAsync(Request, ctk).ConfigureAwait(false);
    }
}

/// <summary>Minimal default <see cref="IUserContext"/> for the sample.</summary>
public sealed class DefaultUserContext : IUserContext
{
    /// <inheritdoc />
    public string? UserId => "anonymous";

    /// <inheritdoc />
    public string? Tenant => null;
}
