// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

namespace Ark.Tools.FtpClient.FluentFtp
{
    public sealed class FluentFtpClientFactory : DefaultFtpClientFactory
    {
        public FluentFtpClientFactory()
            : base(new FluentFtpClientConnectionFactory())
        {
        }
    }
}
