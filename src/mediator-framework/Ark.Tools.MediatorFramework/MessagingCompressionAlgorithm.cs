// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Compression algorithms available to a messaging network.</summary>
public enum MessagingCompressionAlgorithm
{
    /// <summary>No compression.</summary>
    None,

    /// <summary>GZip compression.</summary>
    Gzip,

    /// <summary>Brotli compression.</summary>
    Brotli,
}
