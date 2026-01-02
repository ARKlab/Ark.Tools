// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

using EnsureThat;

namespace Ark.Tools.FtpClient.FluentFtp
{
    public class FluentFtpClientConnectionFactory : IFtpClientConnectionFactory
    {
        public IFtpClientConnection Create(FtpConfig ftpConfig)
        {
            EnsureArg.IsNotNull(ftpConfig);
            EnsureArg.IsNotNull(ftpConfig.Uri);
            EnsureArg.IsNotNull(ftpConfig.Credentials);

            return new FluentFtpClientConnection(ftpConfig);
        }
    }
}