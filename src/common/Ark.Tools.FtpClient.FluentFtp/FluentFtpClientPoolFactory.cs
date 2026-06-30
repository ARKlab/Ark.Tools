// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;

namespace Ark.Tools.FtpClient.FluentFtp;

public sealed class FluentFtpClientPoolFactory : DefaultFtpClientPoolFactory
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FluentFtpClientPoolFactory"/> class.
    /// </summary>
    public FluentFtpClientPoolFactory()
        : base(new FluentFtpClientConnectionFactory())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FluentFtpClientPoolFactory"/> class.
    /// </summary>
    /// <param name="configureClient">
    /// Callback used to customize the FluentFTP client configuration for the requested host before the client is created.
    /// </param>
    public FluentFtpClientPoolFactory(Action<string, FluentFTP.FtpConfig> configureClient)
        : base(new FluentFtpClientConnectionFactory(configureClient))
    {
    }
}