// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Net;

namespace Ark.Tools.FtpClient.Core
{
    public class DefaultFtpClientFactory : IFtpClientFactory
    {
        private readonly IFtpClientConnectionFactory _connectionFactory;

        public DefaultFtpClientFactory(IFtpClientConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        [Obsolete("Use the constructor with URI", false)]
        public IFtpClient Create(string host, NetworkCredential credentials)
        {
            return new FtpClient(host, credentials, 2, _connectionFactory);
        }

        [Obsolete("Use the constructor with FtpConfig", false)]
        public IFtpClient Create(Uri uri, NetworkCredential credentials)
        {
            return new FtpClient(uri, credentials, 2, _connectionFactory);
        }

        public IFtpClient Create(FtpConfig ftpConfig)
        {
            return new FtpClient(ftpConfig, 2, _connectionFactory);
        }
    }
}