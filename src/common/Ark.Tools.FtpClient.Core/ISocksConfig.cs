// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Org.Mentalis.Network.ProxySocket;

#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

namespace Ark.Tools.FtpClient.Core;

public interface ISocksConfig
{
    #if NET10_0_OR_GREATER
    [NotPersonalData("Proxy IP address is network infrastructure metadata, not personal data.")]
    #endif
    string IpAddress { get; }

    int Port { get; }

    #if NET10_0_OR_GREATER

    [UserCredentials]

    #endif
    string UserName { get; }

    #if NET10_0_OR_GREATER

    [UserCredentials]

    #endif
    string Password { get; }

    ProxyTypes Type { get; }
}