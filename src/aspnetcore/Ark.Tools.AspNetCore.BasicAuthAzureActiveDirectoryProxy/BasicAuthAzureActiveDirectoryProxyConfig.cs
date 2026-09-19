// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Compliance;

namespace Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy;

public class BasicAuthAzureActiveDirectoryProxyConfig
{
    public BasicAuthAzureActiveDirectoryProxyConfig(string tenant, string resource, string proxyClientId, [Secret] string proxyClientSecret)
    {
        Tenant = tenant;
        Resource = resource;
        ProxyClientId = proxyClientId;
        ProxyClientSecret = proxyClientSecret;
    }

    public string Tenant { get; set; }
    public string Resource { get; set; }
    public string ProxyClientId { get; set; }
    [Secret]
    public string ProxyClientSecret { get; set; }
}