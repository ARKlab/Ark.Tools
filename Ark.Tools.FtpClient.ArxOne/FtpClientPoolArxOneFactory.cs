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
        private readonly Action<FtpConfig, FtpClientParameters>? _configurer;

        public FtpClientPoolArxOneFactory(Action<FtpConfig, FtpClientParameters>? configurer = null)
        {
            _configurer = configurer;
        }

        public IFtpClientPool Create(int maxPoolSize, FtpConfig ftpConfig)
        {
            EnsureArg.IsNotNull(ftpConfig);
            EnsureArg.IsNotNull(ftpConfig.Uri);
            EnsureArg.IsNotNull(ftpConfig.Credentials);

            return new FtpClientPoolArxOne(maxPoolSize, ftpConfig, _configurer);
        }
    }
}
