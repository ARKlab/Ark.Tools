// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
using ArxOne.Ftp;
using NLog;
using Org.Mentalis.Network.ProxySocket;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Ark.Tools.FtpClient
{
    public class FtpClientPoolArxOneWithSocks : FtpClientPoolArxOne
    {
        private readonly ISocksConfig _config;
        private readonly FtpClientParameters? _ftpClientParameters;

        public FtpClientPoolArxOneWithSocks(ISocksConfig config, int maxPoolSize, FtpConfig ftpConfig, FtpClientParameters? ftpClientParameters = null)
            : base(maxPoolSize, ftpConfig)
        {
            this._config = config;
            this._ftpClientParameters = ftpClientParameters;
        }

        private protected override ArxOne.Ftp.FtpClient _getClient()
        {
            var ftpClientParameters = _createFtpClientParameters();

            var client = new ArxOne.Ftp.FtpClient(this.Uri, this.Credentials, ftpClientParameters);

            return client;
        }

        private FtpClientParameters _createFtpClientParameters()
        {
            var ftpClientParameters = 
                this._ftpClientParameters != null || this._ftpClientParameters != default(FtpClientParameters) ? 
                this._ftpClientParameters : 
                new FtpClientParameters();

            if (ftpClientParameters.ChannelProtection == null || ftpClientParameters.ChannelProtection == default)
                ftpClientParameters.ConnectTimeout = TimeSpan.FromSeconds(60);

            if (ftpClientParameters.ProxyConnect == null || ftpClientParameters.ProxyConnect == default)
            {
                ftpClientParameters.ProxyConnect = e =>
                {
                    var s = new ProxySocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        ProxyEndPoint = new IPEndPoint(IPAddress.Parse(_config.IpAddress), _config.Port),
                        ProxyUser = _config.UserName,
                        ProxyPass = _config.Password,
                        ProxyType = _config.Type
                    };

                    switch (e)
                    {
                        case DnsEndPoint dns:
                            s.Connect(dns.Host, dns.Port);
                            break;
                        case IPEndPoint ip:
                            s.Connect(ip);
                            break;

                        default: throw new NotSupportedException();
                    }

                    return s;
                };
            }

            return ftpClientParameters;
        }
    }
}
