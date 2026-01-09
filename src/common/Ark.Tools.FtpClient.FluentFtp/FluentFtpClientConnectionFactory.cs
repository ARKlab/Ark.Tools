// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.FtpClient.FluentFtp(net10.0)', Before:
namespace Ark.Tools.FtpClient.FluentFtp
{
    public class FluentFtpClientConnectionFactory : IFtpClientConnectionFactory
    {
        public IFtpClientConnection Create(FtpConfig ftpConfig)
        {
            ArgumentNullException.ThrowIfNull(ftpConfig);
            ArgumentNullException.ThrowIfNull(ftpConfig.Uri);
            ArgumentNullException.ThrowIfNull(ftpConfig.Credentials);

            return new FluentFtpClientConnection(ftpConfig);
        }
=======
namespace Ark.Tools.FtpClient.FluentFtp;

public class FluentFtpClientConnectionFactory : IFtpClientConnectionFactory
{
    public IFtpClientConnection Create(FtpConfig ftpConfig)
    {
        ArgumentNullException.ThrowIfNull(ftpConfig);
        ArgumentNullException.ThrowIfNull(ftpConfig.Uri);
        ArgumentNullException.ThrowIfNull(ftpConfig.Credentials);

        return new FluentFtpClientConnection(ftpConfig);
>>>>>>> After



namespace Ark.Tools.FtpClient.FluentFtp;

    public class FluentFtpClientConnectionFactory : IFtpClientConnectionFactory
    {
        public IFtpClientConnection Create(FtpConfig ftpConfig)
        {
            ArgumentNullException.ThrowIfNull(ftpConfig);
            ArgumentNullException.ThrowIfNull(ftpConfig.Uri);
            ArgumentNullException.ThrowIfNull(ftpConfig.Credentials);

            return new FluentFtpClientConnection(ftpConfig);
        }
    }