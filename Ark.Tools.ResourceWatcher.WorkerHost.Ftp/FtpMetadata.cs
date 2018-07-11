// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient;
using Ark.Tools.FtpClient.Core;
using NodaTime;

namespace Ark.Tools.ResourceWatcher.WorkerHost.Ftp
{
    public class FtpMetadata : IResourceMetadata
    {
        internal FtpMetadata(FtpEntry entry)
        {
            Entry = entry;
            ResourceId = entry.FullPath;
            Modified = LocalDateTime.FromDateTime(entry.Modified);
        }

        public FtpEntry Entry { get; }

        public LocalDateTime Modified { get; }

        public string ResourceId { get; }

        public object Extensions => new
        {
            Entry.Name,
            Entry.Size,
        };
    }
}
