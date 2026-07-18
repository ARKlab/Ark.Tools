// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Concurrent;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>In-memory store shared by every transport, proving they hit the same state.</summary>
public interface IGreetingStore
{
    /// <summary>Persists a greeting.</summary>
    Task SaveAsync(GreetingResponse greeting, CancellationToken ctk = default);

    /// <summary>Reads a greeting by id or throws when missing.</summary>
    Task<GreetingResponse> GetAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Attempts to read a greeting by id.</summary>
    Task<GreetingResponse?> TryGetAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Gets the number of stored greetings.</summary>
    Task<int> CountAsync(CancellationToken ctk = default);

    /// <summary>Returns a snapshot of all stored greetings.</summary>
    Task<IReadOnlyCollection<GreetingResponse>> AllAsync(CancellationToken ctk = default);
}

/// <summary>Thread-safe in-memory <see cref="IGreetingStore"/>.</summary>
public sealed class InMemoryGreetingStore : IGreetingStore
{
    private readonly ConcurrentDictionary<Guid, GreetingResponse> _items = new();

    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken ctk = default)
    {
        return Task.FromResult(_items.Count);
    }

    /// <inheritdoc />
    public Task SaveAsync(GreetingResponse greeting, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(greeting);
        _items[greeting.Id] = greeting;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<GreetingResponse> GetAsync(Guid id, CancellationToken ctk = default)
    {
        return _items.TryGetValue(id, out var greeting)
            ? Task.FromResult(greeting)
            : throw new EntityNotFoundException($"Greeting '{id}' was not found.");
    }

    /// <inheritdoc />
    public Task<GreetingResponse?> TryGetAsync(Guid id, CancellationToken ctk = default)
    {
        _items.TryGetValue(id, out var greeting);
        return Task.FromResult(greeting);
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<GreetingResponse>> AllAsync(CancellationToken ctk = default)
    {
        return Task.FromResult<IReadOnlyCollection<GreetingResponse>>(_items.Values.ToArray());
    }
}
