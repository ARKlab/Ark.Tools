// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

namespace Ark.Tools.FtpClient.FtpProxy;

public class FtpClientPoolProxyFactory : IFtpClientPoolFactory
{
    private readonly IFtpClientProxyConfig _config;
    #if NET10_0_OR_GREATER
    [Secret]
    #endif
    private readonly TokenProvider _tokenProvider;

    public FtpClientPoolProxyFactory(IFtpClientProxyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _tokenProvider = new TokenProvider(config);
    }

    public IFtpClientPool Create(int maxPoolSize, FtpConfig ftpConfig)
    {
        return new FtpClientProxy(_config, _tokenProvider, ftpConfig);
    }
}