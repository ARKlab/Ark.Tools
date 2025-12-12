// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

using EnsureThat;

namespace Ark.Tools.FtpClient.FtpProxy
{
    public class FtpClientProxyFactory : IFtpClientFactory
    {
        private readonly IFtpClientProxyConfig _config;
        private readonly TokenProvider _tokenProvider;

        public FtpClientProxyFactory(IFtpClientProxyConfig config)
        {
            EnsureArg.IsNotNull(config);
            _config = config;
            _tokenProvider = new TokenProvider(config);
        }

        public IFtpClient Create(FtpConfig ftpConfig)
        {
            return new FtpClientProxy(_config, _tokenProvider, ftpConfig);
        }
    }
}
