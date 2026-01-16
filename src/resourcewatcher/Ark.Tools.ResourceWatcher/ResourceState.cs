// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;


namespace Ark.Tools.ResourceWatcher;

/// <summary>
/// Resource state tracking with type-safe extension data.
/// </summary>
/// <typeparam name="TExtensions">The type of extension data. Use <see cref="VoidExtensions"/> if no extension data is needed.</typeparam>
public class ResourceState<TExtensions> : IResourceTrackedState<TExtensions>
    where TExtensions : class
{
    /// <summary>
    /// The tenant identifier
    /// </summary>
    public virtual string? Tenant { get; set; }
    /// <summary>
    /// The resource identifier
    /// </summary>
    public virtual string ResourceId { get; set; } = string.Empty;
    /// <summary>
    /// Checksum for the resource content
    /// </summary>
    public virtual string? CheckSum { get; set; }
    /// <summary>
    /// The version of the resource
    /// </summary>
    public virtual LocalDateTime Modified { get; set; }
    /// <summary>
    /// The versions of the resource per source
    /// </summary>
    public virtual Dictionary<string, LocalDateTime>? ModifiedSources { get; set; }
    /// <summary>
    /// Timestamp of the last event
    /// </summary>
    public virtual Instant LastEvent { get; set; }
    /// <summary>
    /// Timestamp when the resource was retrieved
    /// </summary>
    public virtual Instant? RetrievedAt { get; set; }
    /// <summary>
    /// Number of times processing has been retried
    /// </summary>
    public virtual int RetryCount { get; set; }
    /// <summary>
    /// Type-safe extension data
    /// </summary>
    public virtual TExtensions? Extensions { get; set; }
    /// <summary>
    /// Last exception that occurred during processing
    /// </summary>
    public virtual Exception? LastException { get; set; }
}

/// <summary>
/// Non-generic resource state for backward compatibility.
/// Uses <see cref="VoidExtensions"/> for extension data.
/// </summary>
public class ResourceState : ResourceState<VoidExtensions>
{
}