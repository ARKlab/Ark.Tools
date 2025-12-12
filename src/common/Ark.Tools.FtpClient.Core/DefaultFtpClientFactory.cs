// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.FtpClient.Core
{
    public class DefaultFtpClientFactory : IFtpClientFactory
    {
        private readonly IFtpClientConnectionFactory _connectionFactory;

        public DefaultFtpClientFactory(IFtpClientConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IFtpClient Create(FtpConfig ftpConfig)
        {
            return new FtpClient(ftpConfig, 2, _connectionFactory);
        }
    }
}