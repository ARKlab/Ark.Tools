// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
using ArxOne.Ftp;
using Org.Mentalis.Network.ProxySocket;
using System;
using System.Net;
using System.Net.Sockets;

namespace Ark.Tools.FtpClient
{
    public class FtpClientPoolArxOneWithSocksAndConfigurer : FtpClientPoolArxOne
    {
        private readonly IArxOneConfig _config;

        public FtpClientPoolArxOneWithSocksAndConfigurer(IArxOneConfig config, int maxPoolSize, FtpConfig ftpConfig)
            : base(maxPoolSize, ftpConfig, config.Configurer)
        {
            this._config = config;
        }

        private protected override ArxOne.Ftp.FtpClient _getClient(Action<FtpClientParameters>? configurer)
        {
            var ftpClientParameters = new FtpClientParameters()
            {
                ConnectTimeout = TimeSpan.FromSeconds(60),
                ReadWriteTimeout = TimeSpan.FromMinutes(3),
                Passive = true,
                ProxyConnect = e =>
                {
                    var s = new ProxySocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        ProxyEndPoint = new IPEndPoint(IPAddress.Parse(_config.SocksConfig.IpAddress), _config.SocksConfig.Port),
                        ProxyUser = _config.SocksConfig.UserName,
                        ProxyPass = _config.SocksConfig.Password,
                        ProxyType = _config.SocksConfig.Type
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
                }
            };

            if (configurer != null)
                configurer(ftpClientParameters);
            
            return new ArxOne.Ftp.FtpClient(this.Uri, this.Credentials, ftpClientParameters);
        }
    }
}
