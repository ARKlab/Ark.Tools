// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Net;

namespace Ark.Tools.FtpClient.Core
{

    public interface IFtpClientFactory
    {
        IFtpClient Create(FtpConfig ftpConfig);
    }
}