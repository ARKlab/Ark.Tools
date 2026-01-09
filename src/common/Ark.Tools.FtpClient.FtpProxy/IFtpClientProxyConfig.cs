// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.FtpClient.FtpProxy;


public interface IFtpClientProxyConfig
{
    string ClientID { get; }
    string ClientKey { get; }
    Uri FtpProxyWebInterfaceBaseUri { get; }
    string ApiIdentifier { get; }
    string TenantID { get; }
    bool UseAuth0 { get; }
    int? ListingDegreeOfParallelism { get; }
}