// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

namespace Ark.Tools.FtpClient.SftpClient
{
    public sealed class SFtpClientFactory : DefaultFtpClientFactory
    {
        public SFtpClientFactory() 
            : base(new SFtpClientConnectionFactory())
        {
        }
    }

}
