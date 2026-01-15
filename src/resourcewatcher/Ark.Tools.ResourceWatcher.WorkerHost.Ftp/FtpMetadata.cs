// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

using NodaTime;


namespace Ark.Tools.ResourceWatcher.WorkerHost.Ftp;

public sealed class FtpMetadata : IResourceMetadata
{
    internal FtpMetadata(FtpEntry entry)
    {
        Entry = entry;
        ResourceId = entry.FullPath;
        Modified = LocalDateTime.FromDateTime(entry.Modified);
        ModifiedSources = null;
    }

    public FtpEntry Entry { get; }

    public LocalDateTime Modified { get; }
    public Dictionary<string, LocalDateTime>? ModifiedSources { get; }

    public string ResourceId { get; }

    public VoidExtensions? Extensions => null;
}