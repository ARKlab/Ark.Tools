// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.FtpClient.ArxOne(net10.0)', Before:
namespace Ark.Tools.FtpClient
{
    public class FtpClientPoolArxOneWithSocksFactory : IFtpClientPoolFactory
    {
        private readonly ISocksConfig _config;

        public FtpClientPoolArxOneWithSocksFactory(ISocksConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            _config = config;
        }

        public IFtpClientPool Create(int maxPoolSize, FtpConfig ftpConfig)
        {
            ArgumentNullException.ThrowIfNull(ftpConfig);
            ArgumentNullException.ThrowIfNull(ftpConfig.Uri);
            ArgumentNullException.ThrowIfNull(ftpConfig.Credentials);

            return new FtpClientPoolArxOne(_config, maxPoolSize, ftpConfig);
        }
=======
namespace Ark.Tools.FtpClient;

public class FtpClientPoolArxOneWithSocksFactory : IFtpClientPoolFactory
{
    private readonly ISocksConfig _config;

    public FtpClientPoolArxOneWithSocksFactory(ISocksConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
    }

    public IFtpClientPool Create(int maxPoolSize, FtpConfig ftpConfig)
    {
        ArgumentNullException.ThrowIfNull(ftpConfig);
        ArgumentNullException.ThrowIfNull(ftpConfig.Uri);
        ArgumentNullException.ThrowIfNull(ftpConfig.Credentials);

        return new FtpClientPoolArxOne(_config, maxPoolSize, ftpConfig);
>>>>>>> After



namespace Ark.Tools.FtpClient;

public class FtpClientPoolArxOneWithSocksFactory : IFtpClientPoolFactory
{
    private readonly ISocksConfig _config;

    public FtpClientPoolArxOneWithSocksFactory(ISocksConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
    }

    public IFtpClientPool Create(int maxPoolSize, FtpConfig ftpConfig)
    {
        ArgumentNullException.ThrowIfNull(ftpConfig);
        ArgumentNullException.ThrowIfNull(ftpConfig.Uri);
        ArgumentNullException.ThrowIfNull(ftpConfig.Credentials);

        return new FtpClientPoolArxOne(_config, maxPoolSize, ftpConfig);
    }
}