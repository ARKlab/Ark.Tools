// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
using EnsureThat;
using System.Net;

namespace Ark.Tools.FtpClient.FluentFtp
{
    public class FluentFtpClientConnectionFactory : IFtpClientConnectionFactory
    {
        public IFtpClientConnection Create(string host, NetworkCredential credentials, int port = 0)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);
            return new FluentFtpClientConnection(host, credentials, port);
        }
    }
}
