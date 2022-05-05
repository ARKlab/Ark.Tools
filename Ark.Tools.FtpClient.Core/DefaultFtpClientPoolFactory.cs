// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
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

        [Obsolete("Use the constructor with URI", false)]
        public IFtpClientPool Create(int maxPoolSize, string host, NetworkCredential credentials)
        {
            return new FtpClientPool(maxPoolSize, host, credentials, _connectionFactory);
        }

        [Obsolete("Use the constructor with FtpConfig", false)]
        public IFtpClientPool Create(int maxPoolSize, Uri uri, NetworkCredential credentials)
        {
            return new FtpClientPool(maxPoolSize, uri, credentials, _connectionFactory);
        }

        public IFtpClientPool Create(int maxPoolSize, FtpConfig ftpConfig)
        {
            return new FtpClientPool(maxPoolSize, ftpConfig, _connectionFactory);
        }
    }
}