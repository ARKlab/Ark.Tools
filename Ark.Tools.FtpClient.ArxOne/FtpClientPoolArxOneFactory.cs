// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

using ArxOne.Ftp;

using EnsureThat;
using System;
using System.Net;
using System.Text;

namespace Ark.Tools.FtpClient
{
    public class FtpClientPoolArxOneFactory : IFtpClientPoolFactory
    {
        public IFtpClientPool Create(int maxPoolSize, Action<FtpConfig, FtpClientParameters> ftpParameters)
        {
            EnsureArg.IsNotNull(ftpParameters);

            return new FtpClientPoolArxOne(maxPoolSize, ftpParameters);
        }
    }
}
