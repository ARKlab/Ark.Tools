// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;


namespace Ark.Tools.FtpClient
{
    public class FtpClientPoolArxOneWithConfigFactory : IFtpClientPoolFactory
    {
        private readonly IArxOneConfig _config;

        public FtpClientPoolArxOneWithConfigFactory(IArxOneConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            _config = config;
        }

        public IFtpClientPool Create(int maxPoolSize, FtpConfig ftpConfig)
        {
            ArgumentNullException.ThrowIfNull(ftpConfig);
            ArgumentNullException.ThrowIfNull(ftpConfig.Uri);
            ArgumentNullException.ThrowIfNull(ftpConfig.Credentials);

            return new FtpClientPoolArxOne(_config, maxPoolSize, ftpConfig);
        }
    }
}