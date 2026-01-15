// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.ResourceWatcher.WorkerHost.Ftp;

/// <summary>
/// Extension data for FTP resources.
/// Contains additional metadata about the FTP file.
/// </summary>
public sealed class FtpResourceExtensions
{
    /// <summary>
    /// Gets or sets the name of the file.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public required long Size { get; init; }
}
