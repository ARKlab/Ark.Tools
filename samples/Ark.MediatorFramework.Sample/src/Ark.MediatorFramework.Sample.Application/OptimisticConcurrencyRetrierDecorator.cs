// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Retries request handlers when a concurrent write loses a race.</summary>
public sealed class OptimisticConcurrencyRetrierDecorator<TRequest, TResult> : IRequestHandler<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    private readonly IRequestHandler<TRequest, TResult> _inner;

    /// <summary>Initializes a new instance of the <see cref="OptimisticConcurrencyRetrierDecorator{TRequest, TResult}"/> class.</summary>
    public OptimisticConcurrencyRetrierDecorator(IRequestHandler<TRequest, TResult> inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync(TRequest request, CancellationToken ctk = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await _inner.ExecuteAsync(request, ctk).ConfigureAwait(false);
            }
            catch (OptimisticConcurrencyException) when (attempt < 2)
            {
            }
        }
    }
}
