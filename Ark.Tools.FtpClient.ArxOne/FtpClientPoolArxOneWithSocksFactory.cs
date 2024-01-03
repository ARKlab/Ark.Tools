// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
using EnsureThat;
using System;
using System.Net;
using System.Text;
using ArxOne.Ftp;

namespace Ark.Tools.FtpClient
{
    public class FtpClientPoolArxOneWithSocksFactory : IFtpClientPoolFactory
    {
        private readonly ISocksConfig _config;

        public FtpClientPoolArxOneWithSocksFactory(ISocksConfig config)
        {
            EnsureArg.IsNotNull(config);

            _config = config;
        }

        public IFtpClientPool Create(int maxPoolSize, Action<FtpConfig, FtpClientParameters> ftpParameters)
        {
            EnsureArg.IsNotNull(ftpParameters);

            return new FtpClientPoolArxOneWithSocks(_config, maxPoolSize, ftpParameters);
        }
    }
}
