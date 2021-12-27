// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
using EnsureThat;
using System;
using System.Net;

namespace Ark.Tools.FtpClient
{
    public class FtpClientPoolArxOneFactory : IFtpClientPoolFactory
    {
        [Obsolete("Use the constructor with URI", false)]
        public IFtpClientPool Create(int maxPoolSize, string host, NetworkCredential credentials)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);
            return new FtpClientPoolArxOne(maxPoolSize, host, credentials);
        }
        public IFtpClientPool Create(int maxPoolSize, Uri uri, NetworkCredential credentials)
        {
            EnsureArg.IsNotNull(uri);
            EnsureArg.IsNotNull(credentials);
            return new FtpClientPoolArxOne(maxPoolSize, uri, credentials);
        }
    }
}
