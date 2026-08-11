// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using NLog;

using Polly;

namespace Ark.MediatorFramework.Sample.Application.Services.Decorators;

/// <summary>Retries handlers when the server detects a transient optimistic-concurrency race.</summary>
public sealed class OptimisticConcurrencyRetrierDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly IAsyncPolicy RetryPolicy = Policy
        .Handle<Exception>(static ex => IsOptimistic(ex))
        .RetryAsync(2, onRetry: static (_, attempt) =>
            Logger.Warn(CultureInfo.InvariantCulture,
                "Retrying optimistic-concurrency request {RequestType}, attempt {Attempt}.",
                typeof(TRequest).FullName,
                attempt));

    private readonly IRequestHandler<TRequest, TResponse> _inner;

    /// <summary>Initializes a new instance of the <see cref="OptimisticConcurrencyRetrierDecorator{TRequest, TResponse}"/> class.</summary>
    public OptimisticConcurrencyRetrierDecorator(IRequestHandler<TRequest, TResponse> inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public async Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ctk = default)
    {
        return await RetryPolicy
            .ExecuteAsync(ct => _inner.ExecuteAsync(request, ct), ctk)
            .ConfigureAwait(false);
    }

    private static bool IsOptimistic(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is Tools.Core.OptimisticConcurrencyException)
                return true;
            exception = exception.InnerException;
        }
        return false;
    }
}
