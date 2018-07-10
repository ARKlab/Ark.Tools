// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy
{
    public class BasicAuthAzureActiveDirectoryProxyConfig
    {
        public string Tenant { get; set; }
        public string Resource { get; set; }
        public string ProxyClientId { get; set; }
        public string ProxyClientSecret { get; set; }
    }
}