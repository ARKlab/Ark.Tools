// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

namespace Ark.Tools.ResourceWatcher.WorkerHost.Ftp
{
    public sealed class FtpFile<TPayload> : IResource<FtpMetadata>
    {
        public FtpFile(FtpMetadata metadata)
        {
            Metadata = metadata;
        }

        public FtpMetadata Metadata { get; }

        public Instant RetrievedAt { get; internal set; }

        public LocalDateTime? LastModified { get; internal set; }

        public string? CheckSum { get; internal set; }

        public TPayload? ParsedData { get; internal set; }
    }
}
