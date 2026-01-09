// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.FtpClient.Core(net10.0)', Before:
namespace Ark.Tools.FtpClient.Core
{
    public sealed class FtpClient : FtpClientWithConnectionBase
    {
        private readonly IFtpClientConnectionFactory _connectionFactory;

        public FtpClient(FtpConfig ftpConfig, IFtpClientConnectionFactory connectionFactory)
            : base(ftpConfig)
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
            return Task.FromResult(_connectionFactory.Create(FtpConfig));
        }
=======
namespace Ark.Tools.FtpClient.Core;

public sealed class FtpClient : FtpClientWithConnectionBase
{
    private readonly IFtpClientConnectionFactory _connectionFactory;

    public FtpClient(FtpConfig ftpConfig, IFtpClientConnectionFactory connectionFactory)
        : base(ftpConfig)
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
        return Task.FromResult(_connectionFactory.Create(FtpConfig));
>>>>>>> After


namespace Ark.Tools.FtpClient.Core;

public sealed class FtpClient : FtpClientWithConnectionBase
{
    private readonly IFtpClientConnectionFactory _connectionFactory;

    public FtpClient(FtpConfig ftpConfig, IFtpClientConnectionFactory connectionFactory)
        : base(ftpConfig)
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
        return Task.FromResult(_connectionFactory.Create(FtpConfig));
    }
}