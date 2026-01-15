// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

namespace Ark.Tools.ResourceWatcher;

/// <summary>
/// Interface for tracked resource state with type-safe extension data.
/// </summary>
/// <typeparam name="TExtensions">The type of extension data. Use <see cref="VoidExtensions"/> if no extension data is needed.</typeparam>
public interface IResourceTrackedState<TExtensions> : IResourceMetadata<TExtensions>
    where TExtensions : class
{
    /// <summary>
    /// Number of times processing has been retried
    /// </summary>
    int RetryCount { get; }
    /// <summary>
    /// Timestamp of the last event
    /// </summary>
    Instant LastEvent { get; }
    /// <summary>
    /// Checksum for the resource content
    /// </summary>
    string? CheckSum { get; }
    /// <summary>
    /// Timestamp when the resource was retrieved
    /// </summary>
    Instant? RetrievedAt { get; }
}

/// <summary>
/// Non-generic proxy interface for backward compatibility.
/// Inherits from <see cref="IResourceTrackedState{TExtensions}"/> with <see cref="VoidExtensions"/>.
/// </summary>
public interface IResourceTrackedState : IResourceTrackedState<VoidExtensions>
{
}