// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
using EnsureThat;
using System;
using System.Net;
using System.Text;
using ArxOne.Ftp;
using Ark.Tools.ResourceWatcher.WorkerHost.Ftp;

namespace Ark.Tools.FtpClient
{
    public class FtpClientPoolArxOneWithSocksFactory : IFtpClientPoolFactory
    {
        private readonly ISocksConfig _config;
        private readonly Action<FtpClientParameters>? _configurer;

        public FtpClientPoolArxOneWithSocksFactory(ISocksConfig config, Action<FtpClientParameters>? configurer = null)
        {
            EnsureArg.IsNotNull(config);

            _config = config;
            _configurer = configurer;
        }

        public IFtpClientPool Create(int maxPoolSize, FtpConfig ftpConfig)
        {
            EnsureArg.IsNotNull(ftpConfig);
            EnsureArg.IsNotNull(ftpConfig.Uri);
            EnsureArg.IsNotNull(ftpConfig.Credentials);

            return new FtpClientPoolArxOneWithSocks(_config, maxPoolSize, ftpConfig, _configurer);
        }
    }
}
