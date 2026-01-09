// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Org.Mentalis.Network.ProxySocket;

namespace Ark.Tools.FtpClient.Core;

public interface ISocksConfig
{
    string IpAddress { get; }
    int Port { get; }
    string UserName { get; }
    string Password { get; }
    ProxyTypes Type { get; }
}