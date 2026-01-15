// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

namespace Ark.Tools.ResourceWatcher;

/// <summary>
/// Marker type for resources that don't use extensions.
/// Use this as the TExtensions parameter when no extension data is needed.
/// </summary>
/// <remarks>
/// This type is a singleton class that serializes to JSON null.
/// Use <see cref="Instance"/> to get the singleton instance.
/// </remarks>
public sealed class VoidExtensions
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static VoidExtensions Instance { get; } = new VoidExtensions();

    private VoidExtensions()
    {
    }
}

/// <summary>
/// Metadata interface for resources with type-safe extension data.
/// </summary>
/// <typeparam name="TExtensions">The type of extension data. Use <see cref="VoidExtensions"/> if no extension data is needed.</typeparam>
public interface IResourceMetadata<TExtensions>
    where TExtensions : class
{
    /// <summary>
    /// The "key" identifier of the resource
    /// </summary>
    string ResourceId { get; }
    /// <summary>
    /// The "version" of the resource. Used to avoid retrival of the resource in case if the same version is already been processed successfully
    /// This field is alternative to ModifiedSources field
    /// </summary>
    LocalDateTime Modified { get; }
    /// <summary>
    /// The "versions" of the resource. Used to manage multiple sources for the resource.
    /// The resource will be processed when a new source will be add or at least one source have an updated modified.
    /// </summary>
    /// <remarks>
    /// The keys are lowercase
    /// </remarks>
    Dictionary<string, LocalDateTime>? ModifiedSources { get => null; }
    /// <summary>
    /// Additional info serialized to the State tracking
    /// </summary>
    TExtensions? Extensions { get; }
}

/// <summary>
/// Non-generic proxy interface for backward compatibility.
/// Inherits from <see cref="IResourceMetadata{TExtensions}"/> with <see cref="VoidExtensions"/>.
/// </summary>
public interface IResourceMetadata : IResourceMetadata<VoidExtensions>
{
}