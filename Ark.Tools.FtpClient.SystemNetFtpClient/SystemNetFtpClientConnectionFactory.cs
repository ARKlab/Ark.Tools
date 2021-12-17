// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Net;

namespace Ark.Tools.FtpClient.SystemNetFtpClient
{
    using Ark.Tools.FtpClient.Core;
    using EnsureThat;

    public class SystemNetFtpClientConnectionFactory : IFtpClientConnectionFactory
    {
        public Ark.Tools.FtpClient.Core.IFtpClientConnection Create(string host, NetworkCredential credentials, int port = 0)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);
            return new SystemNetFtpClientConnection(host, credentials, port);
        }
    }
}
