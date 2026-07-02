// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Concurrent;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>In-memory store shared by every transport, proving they hit the same state.</summary>
public interface IGreetingStore
{
    /// <summary>Persists a greeting.</summary>
    void Save(GreetingResponse greeting);

    /// <summary>Reads a greeting by id or throws when missing.</summary>
    GreetingResponse Get(Guid id);

    /// <summary>Attempts to read a greeting by id.</summary>
    bool TryGet(Guid id, out GreetingResponse? greeting);

    /// <summary>Gets the number of stored greetings.</summary>
    int Count { get; }

    /// <summary>Returns a snapshot of all stored greetings.</summary>
    IReadOnlyCollection<GreetingResponse> All();
}

/// <summary>Thread-safe in-memory <see cref="IGreetingStore"/>.</summary>
public sealed class InMemoryGreetingStore : IGreetingStore
{
    private readonly ConcurrentDictionary<Guid, GreetingResponse> _items = new();

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <inheritdoc />
    public void Save(GreetingResponse greeting)
    {
        ArgumentNullException.ThrowIfNull(greeting);
        _items[greeting.Id] = greeting;
    }

    /// <inheritdoc />
    public GreetingResponse Get(Guid id)
        => _items.TryGetValue(id, out var g)
            ? g
            : throw new KeyNotFoundException($"Greeting '{id}' was not found.");

    /// <inheritdoc />
    public bool TryGet(Guid id, out GreetingResponse? greeting)
        => _items.TryGetValue(id, out greeting);

    /// <inheritdoc />
    public IReadOnlyCollection<GreetingResponse> All()
        => _items.Values.ToArray();
}
