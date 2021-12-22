// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
using Ark.Tools.Http;

using EnsureThat;
using System;
using System.Net;

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

        public IFtpClient Create(string host, NetworkCredential credentials)
        {
            return new FtpClientProxy(_config, ArkFlurlClientFactory.Instance, _tokenProvider, host, credentials);
        }

        public IFtpClient Create(Uri uri, NetworkCredential credentials)
        {
            return new FtpClientProxy(_config, ArkFlurlClientFactory.Instance, _tokenProvider, uri, credentials);
        }
    }
}
