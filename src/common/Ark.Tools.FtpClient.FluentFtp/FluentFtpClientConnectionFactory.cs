// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;



namespace Ark.Tools.FtpClient.FluentFtp;

public class FluentFtpClientConnectionFactory : IFtpClientConnectionFactory
{
    private readonly Action<string, FluentFTP.FtpConfig>? _configureClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="FluentFtpClientConnectionFactory"/> class.
    /// </summary>
    public FluentFtpClientConnectionFactory()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FluentFtpClientConnectionFactory"/> class.
    /// </summary>
    /// <param name="configureClient">
    /// Callback used to customize the FluentFTP client configuration for the requested host before the client is created.
    /// </param>
    public FluentFtpClientConnectionFactory(Action<string, FluentFTP.FtpConfig> configureClient)
    {
        _configureClient = configureClient ?? throw new ArgumentNullException(nameof(configureClient));
    }

    /// <inheritdoc />
    public IFtpClientConnection Create(FtpConfig ftpConfig)
    {
        ArgumentNullException.ThrowIfNull(ftpConfig);
        ArgumentNullException.ThrowIfNull(ftpConfig.Uri);
        ArgumentNullException.ThrowIfNull(ftpConfig.Credentials);

        return new FluentFtpClientConnection(ftpConfig, _configureClient);
    }
}