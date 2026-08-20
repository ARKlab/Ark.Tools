// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Optional sender-side payload compression algorithms.</summary>
public enum CompressionAlgorithm
{
    /// <summary>Do not compress payloads.</summary>
    None,

    /// <summary>Gzip compression.</summary>
    Gzip,

    /// <summary>Brotli compression.</summary>
    Brotli
}
