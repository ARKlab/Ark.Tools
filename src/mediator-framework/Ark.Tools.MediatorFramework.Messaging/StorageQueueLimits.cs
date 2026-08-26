// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Defines the fixed Azure Storage Queue envelope size limits.</summary>
public static class StorageQueueLimits
{
    /// <summary>Gets the canonical bytes reserved for bounded poison metadata.</summary>
    public const int PoisonMetadataReservedBytes = 3_072;

    /// <summary>Gets the maximum canonical bytes for a normal inline envelope.</summary>
    public const int MaximumNormalCanonicalBytes = 46_080;

    /// <summary>Gets the maximum canonical bytes for a poison envelope.</summary>
    public const int MaximumPoisonCanonicalBytes =
        MaximumNormalCanonicalBytes + PoisonMetadataReservedBytes;

    /// <summary>Gets the maximum final Base64 text size accepted by Azure Storage Queue.</summary>
    public const int MaximumEncodedTextBytes = 65_536;
}
