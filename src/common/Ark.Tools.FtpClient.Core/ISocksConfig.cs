// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Org.Mentalis.Network.ProxySocket;

using Ark.Tools.Compliance;

namespace Ark.Tools.FtpClient.Core;

public interface ISocksConfig
{
    [NotPersonalData("Proxy IP address is network infrastructure metadata, not personal data.")]
    string IpAddress { get; }

    int Port { get; }

    [UserCredentials]
    string UserName { get; }

    [UserCredentials]
    string Password { get; }

    ProxyTypes Type { get; }
}