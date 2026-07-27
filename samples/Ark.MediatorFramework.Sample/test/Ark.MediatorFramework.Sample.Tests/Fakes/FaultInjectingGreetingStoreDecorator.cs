// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;

using Ark.Tools.Core;

namespace Ark.MediatorFramework.Sample.Tests.Fakes;

/// <summary>Injects deterministic optimistic-concurrency failures for sample test scenarios.</summary>
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
            if (pending <= 0)
                return;
            if (Interlocked.CompareExchange(ref _pendingFailures, pending - 1, pending) == pending)
                throw new OptimisticConcurrencyException("Synthetic optimistic-concurrency failure.");
        }
    }
}

/// <summary>Wraps <see cref="IGreetingStore"/> and injects deterministic concurrency failures before updates.</summary>
public sealed class FaultInjectingGreetingStoreDecorator : IGreetingStore
{
    private readonly IGreetingStore _inner;
    private readonly ConcurrencyFaultInjector _faults;

    /// <summary>Initializes a new instance of the <see cref="FaultInjectingGreetingStoreDecorator"/> class.</summary>
    public FaultInjectingGreetingStoreDecorator(IGreetingStore inner, ConcurrencyFaultInjector faults)
    {
        _inner = inner;
        _faults = faults;
    }

    /// <inheritdoc />
    public Task<GreetingResponse> UpdateAsync(Guid id, string message, string? expectedETag, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        _faults.ThrowIfPending();
        return _inner.UpdateAsync(id, message, expectedETag, audit, ctk);
    }

    /// <inheritdoc />
    public Task SaveAsync(GreetingResponse greeting, AuditEntry? audit = null, CancellationToken ctk = default)
        => _inner.SaveAsync(greeting, audit, ctk);

    /// <inheritdoc />
    public Task SaveAndPublishAsync(GreetingResponse greeting, AuditEntry? audit = null, CancellationToken ctk = default)
        => _inner.SaveAndPublishAsync(greeting, audit, ctk);

    /// <inheritdoc />
    public Task<PagedResult<AuditRecord>> ReadAuditsAsync(GetAuditsQuery query, CancellationToken ctk = default)
        => _inner.ReadAuditsAsync(query, ctk);

    /// <inheritdoc />
    public Task<GreetingPage> ReadGreetingsAsync(SearchGreetingsQuery query, CancellationToken ctk = default)
        => _inner.ReadGreetingsAsync(query, ctk);

    /// <inheritdoc />
    public Task<GreetingResponse> GetAsync(Guid id, CancellationToken ctk = default)
        => _inner.GetAsync(id, ctk);

    /// <inheritdoc />
    public Task<GreetingResponse?> TryGetAsync(Guid id, CancellationToken ctk = default)
        => _inner.TryGetAsync(id, ctk);

    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken ctk = default)
        => _inner.CountAsync(ctk);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<GreetingResponse>> AllAsync(CancellationToken ctk = default)
        => _inner.AllAsync(ctk);
}
