// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
using EnsureThat;
using System;
using System.Net;
using System.Text.RegularExpressions;

namespace Ark.Tools.FtpClient.SftpClient
{
    public sealed class SFtpClientConnectionFactory : IFtpClientConnectionFactory
    {
        public IFtpClientConnection Create(string host, NetworkCredential credentials, int port = 0)
        {
            EnsureArg.IsNotEmpty(host);
            EnsureArg.IsNotNull(credentials);

            string h = host;
            var r = Regex.Match(host, @":\d+$");
            if (r.Success)
            {
                h = host.Substring(0, r.Index);
                port = Convert.ToInt16(host.Substring(r.Index + 1));
            }

            return new SftpClientConnection(h, credentials, port);
        }
    }

}
