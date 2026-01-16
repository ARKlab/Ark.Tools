// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.ResourceWatcher.WorkerHost;

using NodaTime;

namespace Ark.Tools.ResourceWatcher.Testing;


/// <summary>
/// Stub resource for testing purposes.
/// Contains metadata and a data payload.
/// </summary>
/// <typeparam name="TExtensions">The type of extension data stored with the resource metadata.</typeparam>
public class StubResource<TExtensions> : IResource<StubResourceMetadata<TExtensions>, TExtensions>
    where TExtensions : class
{
    /// <summary>
    /// Gets or sets the resource metadata.
    /// </summary>
    public required StubResourceMetadata<TExtensions> Metadata { get; init; }

    /// <summary>
    /// Gets or sets the resource data.
    /// </summary>
    public required string Data { get; init; }

    /// <summary>
    /// Gets or sets the resource checksum.
    /// </summary>
    public string? CheckSum { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when the resource was retrieved.
    /// </summary>
    public Instant RetrievedAt { get; init; }
}

/// <summary>
/// Non-generic proxy class for backward compatibility.
/// Inherits from <see cref="StubResource{TExtensions}"/> with <see cref="VoidExtensions"/>.
/// </summary>
public sealed class StubResource : StubResource<VoidExtensions>
{
}