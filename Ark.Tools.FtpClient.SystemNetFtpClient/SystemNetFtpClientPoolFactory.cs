// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.FtpClient.SystemNetFtpClient
{
    using Ark.Tools.FtpClient.Core;

    public sealed class SystemNetFtpClientPoolFactory : DefaultFtpClientPoolFactory
    {
        public SystemNetFtpClientPoolFactory()
            : base(new SystemNetFtpClientConnectionFactory())
        {
        }
    }
}
