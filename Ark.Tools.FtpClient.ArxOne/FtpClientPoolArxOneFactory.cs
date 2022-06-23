// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
using EnsureThat;
using System;
using System.Net;

namespace Ark.Tools.FtpClient
{
    public class FtpClientPoolArxOneFactory : IFtpClientPoolFactory
    {
        public IFtpClientPool Create(int maxPoolSize, FtpConfig ftpConfig)
        {
            EnsureArg.IsNotNull(ftpConfig);
            EnsureArg.IsNotNull(ftpConfig.Uri);
            EnsureArg.IsNotNull(ftpConfig.Credentials);

            return new FtpClientPoolArxOne(maxPoolSize, ftpConfig);
        }
    }
}
