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

        [Obsolete("Use the constructor with URI", false)]
        public FtpClient(string host, NetworkCredential credential, IFtpClientConnectionFactory connectionFactory) 
            : base(host, credential)
        {
            _connectionFactory = connectionFactory;
        }

        [Obsolete("Use the constructor with FtpConfig", false)]
        public FtpClient(Uri uri, NetworkCredential credential, IFtpClientConnectionFactory connectionFactory)
            : base(uri, credential)
        {
            _connectionFactory = connectionFactory;
        }

        public FtpClient(FtpConfig ftpConfig, IFtpClientConnectionFactory connectionFactory)
            : base(ftpConfig)
        {
            _connectionFactory = connectionFactory;
        }

        [Obsolete("Use the constructor with URI", false)]
        public FtpClient(string host, NetworkCredential credential, int maxListingParallelism, IFtpClientConnectionFactory connectionFactory)
            : base(host, credential, maxListingParallelism)
        {
            _connectionFactory = connectionFactory;
        }

        [Obsolete("Use the constructor with FtpConfig", false)]
        public FtpClient(Uri uri, NetworkCredential credential, int maxListingParallelism, IFtpClientConnectionFactory connectionFactory)
            : base(uri, credential, maxListingParallelism)
        {
            _connectionFactory = connectionFactory;
        }

        public FtpClient(FtpConfig ftpConfig, int maxListingParallelism, IFtpClientConnectionFactory connectionFactory)
            : base(ftpConfig, maxListingParallelism)
        {
            _connectionFactory = connectionFactory;
        }

        protected override Task<IFtpClientConnection> GetConnection(CancellationToken ctk = default)
        {
            if (Uri == null)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                return Task.FromResult(_connectionFactory.Create(Host, Credentials));
#pragma warning restore CS0618 // Type or member is obsolete
            }
            else
                return Task.FromResult(_connectionFactory.Create(FtpConfig));
        }
    }
}