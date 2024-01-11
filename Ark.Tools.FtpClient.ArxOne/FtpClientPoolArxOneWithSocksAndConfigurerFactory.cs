// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
using EnsureThat;
using System;
using ArxOne.Ftp;

namespace Ark.Tools.FtpClient
{
    public class FtpClientPoolArxOneWithSocksAndConfigurerFactory : IFtpClientPoolFactory
    {
        private readonly IArxOneConfig _config;

        public FtpClientPoolArxOneWithSocksAndConfigurerFactory(IArxOneConfig config)
        {
            EnsureArg.IsNotNull(config);

            _config = config;
        }

        public IFtpClientPool Create(int maxPoolSize, FtpConfig ftpConfig)
        {
            EnsureArg.IsNotNull(ftpConfig);
            EnsureArg.IsNotNull(ftpConfig.Uri);
            EnsureArg.IsNotNull(ftpConfig.Credentials);

            return new FtpClientPoolArxOneWithSocksAndConfigurer(_config, maxPoolSize, ftpConfig);
        }
    }
}
