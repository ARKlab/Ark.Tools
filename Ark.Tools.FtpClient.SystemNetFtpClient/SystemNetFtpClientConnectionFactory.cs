// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Net;

namespace Ark.Tools.FtpClient.SystemNetFtpClient
{
    using Ark.Tools.FtpClient.Core;
    using EnsureThat;
    using System;

    public class SystemNetFtpClientConnectionFactory : IFtpClientConnectionFactory
    {
        [Obsolete("Use the constructor with URI", false)]
        public IFtpClientConnection Create(string host, NetworkCredential credentials)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);
            return new SystemNetFtpClientConnection(host, credentials);
        }

        [Obsolete("Use the constructor with FtpConfig", false)]
        public IFtpClientConnection Create(Uri uri, NetworkCredential credentials)
        {
            EnsureArg.IsNotNull(uri);
            EnsureArg.IsNotNull(credentials);
            return new SystemNetFtpClientConnection(uri, credentials);
        }

        public IFtpClientConnection Create(FtpConfig ftpConfig)
        {
            EnsureArg.IsNotNull(ftpConfig);
            EnsureArg.IsNotNull(ftpConfig.Uri);
            EnsureArg.IsNotNull(ftpConfig.Credentials);

            return new SystemNetFtpClientConnection(ftpConfig);
        }
    }
}
