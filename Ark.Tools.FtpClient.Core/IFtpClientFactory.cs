// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Net;

namespace Ark.Tools.FtpClient.Core
{

    public interface IFtpClientFactory
    {
        IFtpClient Create(string host, NetworkCredential credentials);
        IFtpClient Create(Uri uri, NetworkCredential credentials);
    }
}