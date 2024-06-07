// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

namespace Ark.Tools.FtpClient.SftpClient
{
    public sealed class SFtpClientPoolFactory : DefaultFtpClientPoolFactory
    {
        public SFtpClientPoolFactory() 
            : base(new SFtpClientConnectionFactory())
        {
        }
    }

}
