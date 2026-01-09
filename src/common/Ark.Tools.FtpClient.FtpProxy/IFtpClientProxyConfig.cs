// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.FtpClient.FtpProxy(net10.0)', Before:
namespace Ark.Tools.FtpClient.FtpProxy
{

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
=======
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
>>>>>>> After


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