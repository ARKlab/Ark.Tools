// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using NLog;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Retries handlers when the server detects a transient optimistic-concurrency race.</summary>
public sealed class OptimisticConcurrencyRetrierDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IRequestHandler<TRequest, TResponse> _inner;

    /// <summary>Initializes a new instance of the <see cref="OptimisticConcurrencyRetrierDecorator{TRequest, TResponse}"/> class.</summary>
    public OptimisticConcurrencyRetrierDecorator(IRequestHandler<TRequest, TResponse> inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public async Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ctk = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await _inner.ExecuteAsync(request, ctk).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < 2 && IsOptimistic(ex))
            {
                Logger.Warn(System.Globalization.CultureInfo.InvariantCulture,
                    "Retrying optimistic-concurrency request {0}, attempt {1}.",
                    typeof(TRequest).FullName,
                    attempt + 1);
            }
        }
    }

    private static bool IsOptimistic(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is Ark.Tools.Core.OptimisticConcurrencyException)
                return true;
            exception = exception.InnerException;
        }
        return false;
    }
}

/// <summary>Injects deterministic optimistic-concurrency failures for sample demonstrations.</summary>
public sealed class ConcurrencyFaultInjector
{
    private int _pendingFailures;

    /// <summary>Gets or sets the number of failures still to inject.</summary>
    public int PendingFailures
    {
        get => Volatile.Read(ref _pendingFailures);
        set => Volatile.Write(ref _pendingFailures, value);
    }

    /// <summary>Throws a synthetic optimistic-concurrency failure when one is pending.</summary>
    public void ThrowIfPending()
    {
        while (true)
        {
            var pending = Volatile.Read(ref _pendingFailures);
            if (pending <= 0 || Interlocked.CompareExchange(ref _pendingFailures, pending - 1, pending) != pending)
                return;
            throw new Ark.Tools.Core.OptimisticConcurrencyException("Synthetic optimistic-concurrency failure.");
        }
    }
}
