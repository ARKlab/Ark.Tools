// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;


namespace Ark.Tools.FtpClient.SftpClient
{
    public sealed class SFtpClientConnectionFactory : IFtpClientConnectionFactory
    {
        public IFtpClientConnection Create(FtpConfig ftpConfig)
        {
            ArgumentNullException.ThrowIfNull(ftpConfig);
            ArgumentNullException.ThrowIfNull(ftpConfig.Uri);
            ArgumentNullException.ThrowIfNull(ftpConfig.Credentials);

            return new SftpClientConnection(ftpConfig);
        }
    }

}