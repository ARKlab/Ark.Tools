// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Compliance;

namespace Ark.Tools.AspNetCore.BasicAuthAuth0Proxy;

public class BasicAuthAuth0ProxyConfig
{
    public BasicAuthAuth0ProxyConfig(string audience, string proxyClientId, string domain, 
    [Secret]
 string proxySecret)
    {
        Audience = audience;
        ProxyClientId = proxyClientId;
        Domain = domain;
        ProxySecret = proxySecret;
    }

    public string Audience { get; set; }
    public string ProxyClientId { get; set; }
    public string Domain { get; set; }
    [Secret]
    public string ProxySecret { get; set; }
    public string? Realm { get; set; }
}