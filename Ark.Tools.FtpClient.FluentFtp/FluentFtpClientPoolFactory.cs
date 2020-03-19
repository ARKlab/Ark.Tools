// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

namespace Ark.Tools.FtpClient.FluentFtp
{
    public sealed class FluentFtpClientPoolFactory : DefaultFtpClientPoolFactory
    {
        public FluentFtpClientPoolFactory()
            : base(new FluentFtpClientConnectionFactory())
        {
        }
    }
}
