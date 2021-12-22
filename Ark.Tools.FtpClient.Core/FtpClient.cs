// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.FtpClient.Core
{
    public sealed class FtpClient : FtpClientWithConnectionBase
    {
        private readonly IFtpClientConnectionFactory _connectionFactory;

        public FtpClient(string host, NetworkCredential credential, IFtpClientConnectionFactory connectionFactory) 
            : base(host, credential)
        {
            _connectionFactory = connectionFactory;
        }

        public FtpClient(Uri uri, NetworkCredential credential, IFtpClientConnectionFactory connectionFactory)
            : base(uri, credential)
        {
            _connectionFactory = connectionFactory;
        }

        public FtpClient(string host, NetworkCredential credential, int maxListingParallelism, IFtpClientConnectionFactory connectionFactory)
            : base(host, credential, maxListingParallelism)
        {
            _connectionFactory = connectionFactory;
        }

        public FtpClient(Uri uri, NetworkCredential credential, int maxListingParallelism, IFtpClientConnectionFactory connectionFactory)
            : base(uri, credential, maxListingParallelism)
        {
            _connectionFactory = connectionFactory;
        }

        protected override Task<IFtpClientConnection> GetConnection(CancellationToken ctk = default)
        {
            if (Uri == null)
            {
                return Task.FromResult(_connectionFactory.Create(Host, Credentials));
            }
            else
                return Task.FromResult(_connectionFactory.Create(Uri, Credentials));
        }
    }
}