// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Net;

namespace Ark.Tools.FtpClient.Core
{
    public interface IFtpClientConnectionFactory
    {
        [Obsolete("Use the constructor with URI", false)]
        IFtpClientConnection Create(string host, NetworkCredential credentials);

        [Obsolete("Use the constructor with FtpConfig", false)]
        IFtpClientConnection Create(Uri uri, NetworkCredential credentials);

        IFtpClientConnection Create(FtpConfig ftpConfig);
    }
}