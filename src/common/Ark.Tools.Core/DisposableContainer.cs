// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Generic;
using System.Threading;

namespace Ark.Tools.Core;

/// <summary>
/// Utility class for tracking and disposing of objects that implement IDisposable.
/// </summary>
public sealed class DisposableContainer : IDisposable
{
    private readonly List<IDisposable> _disposables;
    private const int DefaultCapacity = 16;
    private readonly Lock _gate = new();
    private bool _disposed;

    public DisposableContainer()
    {
        _disposables = new(DefaultCapacity);
    }

    public DisposableContainer(params IDisposable[] disposables)
    {
        ArgumentNullException.ThrowIfNull(disposables);

        _disposables = new(disposables.Length);
        foreach (var d in disposables)
        {
            ArgumentNullException.ThrowIfNull(d);
            _disposables.Add(d);
        }
    }

    /// <summary>
    /// Disposes of all elements of list.
    /// </summary>
    public void Dispose()
    {
        List<IDisposable>? disposables = null;

        lock (_gate)
        {
            if (!_disposed)
            {
                disposables = _disposables;
                _disposables.Clear();
                Volatile.Write(ref _disposed, true);
            }
        }

        if (disposables is not null)
        {
            foreach (var d in disposables)
            {
                d.Dispose();
            }
        }
    }

    /// <summary>
    /// Add an item to the list.
    /// </summary>
    public void Add(IDisposable item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_gate)
        {
            if (!_disposed)
            {
                _disposables.Add(item);
                return;
            }
        }

        item.Dispose();
    }
}