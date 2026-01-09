// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.FtpClient.Core;

public class DefaultFtpClientPoolFactory : IFtpClientPoolFactory
{
    private readonly IFtpClientConnectionFactory _connectionFactory;

    public DefaultFtpClientPoolFactory(IFtpClientConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public IFtpClientPool Create(int maxPoolSize, FtpConfig ftpConfig)
    {
        return new FtpClientPool(maxPoolSize, ftpConfig, _connectionFactory);
    }
}
