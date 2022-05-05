// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Net;

namespace Ark.Tools.FtpClient.Core
{
    public interface IFtpClientPoolFactory
    {
        [Obsolete("Use the constructor with URI", false)]
        IFtpClientPool Create(int maxPoolSize, string host, NetworkCredential credentials);

        [Obsolete("Use the constructor with FtpConfig", false)]
        IFtpClientPool Create(int maxPoolSize, Uri uri, NetworkCredential credentials);

        IFtpClientPool Create(int maxPoolSize, FtpConfig ftpConfig);
    }
}