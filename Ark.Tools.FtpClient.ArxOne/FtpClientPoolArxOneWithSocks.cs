// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NLog;
using ArxOne.Ftp;
using Ark.Tools.FtpClient.Core;
using System;
using System.Net;
using System.Net.Sockets;
using Org.Mentalis.Network.ProxySocket;

namespace Ark.Tools.FtpClient
{
    public class FtpClientPoolArxOneWithSocks : FtpClientPoolArxOne
    {
        private readonly ISocksConfig _config;
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public FtpClientPoolArxOneWithSocks(ISocksConfig config, int maxPoolSize, string host, NetworkCredential credentials) 
            : base(maxPoolSize, host, credentials)
        {
            this._config = config;
        }

        private protected override ArxOne.Ftp.FtpClient _getClient()
        {
            var client = new ArxOne.Ftp.FtpClient(new Uri("ftp://" + this.Host), this.Credentials, new FtpClientParameters()
            {
                ConnectTimeout = TimeSpan.FromSeconds(60),
                ProxyConnect = e =>
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
                }
            });

            return client;
        }
    }
}
