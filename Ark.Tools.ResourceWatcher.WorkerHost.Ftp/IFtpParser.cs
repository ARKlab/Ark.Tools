// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.ResourceWatcher.WorkerHost.Ftp
{
    public interface IFtpParser<TPayload>
    {
        TPayload Parse(FtpMetadata metadata, byte[] contents);
    }
}
