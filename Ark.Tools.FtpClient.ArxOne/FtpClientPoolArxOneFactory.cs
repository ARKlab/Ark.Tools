// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
using EnsureThat;
using System.Net;

namespace Ark.Tools.FtpClient
{
    public class FtpClientPoolArxOneFactory : IFtpClientPoolFactory
    {
        public IFtpClientPool Create(int maxPoolSize, string host, NetworkCredential credentials, int port = 0)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);
            return new FtpClientPoolArxOne(maxPoolSize, host, credentials);
        }
    }
}
