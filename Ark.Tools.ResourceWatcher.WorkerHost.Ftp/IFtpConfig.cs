﻿// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

using ArxOne.Ftp;

using System;
using System.Net;
using System.Text;

namespace Ark.Tools.ResourceWatcher.WorkerHost.Ftp
{
    public interface IFtpConfig
    {
        FtpConfig FtpConfig { get; }
        TimeSpan ListingTimeout { get; }
        TimeSpan DownloadTimeout { get; }
        int MaxConcurrentConnections { get; }
    }
}
