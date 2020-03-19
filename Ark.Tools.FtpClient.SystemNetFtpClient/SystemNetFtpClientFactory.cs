// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.FtpClient.SystemNetFtpClient
{
    using Ark.Tools.FtpClient.Core;

    public sealed class SystemNetFtpClientFactory : DefaultFtpClientFactory
    {
        public SystemNetFtpClientFactory()
            : base(new SystemNetFtpClientConnectionFactory())
        {
        }
    }
}
