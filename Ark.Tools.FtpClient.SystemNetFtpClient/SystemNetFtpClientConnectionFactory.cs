// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Net;

namespace Ark.Tools.FtpClient.SystemNetFtpClient
{
    using Ark.Tools.FtpClient.Core;
    using EnsureThat;
    using System;

    public class SystemNetFtpClientConnectionFactory : IFtpClientConnectionFactory
    {
        public IFtpClientConnection Create(FtpConfig ftpConfig)
        {
            EnsureArg.IsNotNull(ftpConfig);
            EnsureArg.IsNotNull(ftpConfig.Uri);
            EnsureArg.IsNotNull(ftpConfig.Credentials);

            return new SystemNetFtpClientConnection(ftpConfig);
        }
    }
}
