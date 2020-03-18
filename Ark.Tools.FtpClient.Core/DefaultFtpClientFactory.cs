// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
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

        public IFtpClient Create(string host, NetworkCredential credentials)
        {
            return new FtpClient(host, credentials, _connectionFactory);
        }
    }
}