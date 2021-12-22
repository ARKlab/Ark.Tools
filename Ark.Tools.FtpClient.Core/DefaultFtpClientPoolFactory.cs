// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Net;

namespace Ark.Tools.FtpClient.Core
{
    public class DefaultFtpClientPoolFactory : IFtpClientPoolFactory
    {
        private readonly IFtpClientConnectionFactory _connectionFactory;

        public DefaultFtpClientPoolFactory(IFtpClientConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IFtpClientPool Create(int maxPoolSize, string host, NetworkCredential credentials)
        {
            return new FtpClientPool(maxPoolSize, host, credentials, _connectionFactory);
        }
    }
}