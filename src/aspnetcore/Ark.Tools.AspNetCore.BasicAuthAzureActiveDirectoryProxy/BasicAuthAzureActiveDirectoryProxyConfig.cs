// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Compliance;

namespace Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy;

/// <summary>
/// Configures the Azure Active Directory basic-authentication proxy.
/// </summary>
public class BasicAuthAzureActiveDirectoryProxyConfig
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BasicAuthAzureActiveDirectoryProxyConfig"/> class without a client secret.
    /// </summary>
    /// <param name="tenant">The Azure Active Directory tenant.</param>
    /// <param name="resource">The resource requested from Azure Active Directory.</param>
    /// <param name="proxyClientId">The proxy application client identifier.</param>
    public BasicAuthAzureActiveDirectoryProxyConfig(string tenant, string resource, string proxyClientId)
        : this(tenant, resource, proxyClientId, string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicAuthAzureActiveDirectoryProxyConfig"/> class.
    /// </summary>
    /// <param name="tenant">The Azure Active Directory tenant.</param>
    /// <param name="resource">The resource requested from Azure Active Directory.</param>
    /// <param name="proxyClientId">The proxy application client identifier.</param>
    /// <param name="proxyClientSecret">The proxy application client secret.</param>
    public BasicAuthAzureActiveDirectoryProxyConfig(string tenant, string resource, string proxyClientId, [Secret] string proxyClientSecret)
    {
        Tenant = tenant;
        Resource = resource;
        ProxyClientId = proxyClientId;
        ProxyClientSecret = proxyClientSecret;
    }

    /// <summary>Gets or sets the Azure Active Directory tenant.</summary>
    public string Tenant { get; set; }

    /// <summary>Gets or sets the resource requested from Azure Active Directory.</summary>
    public string Resource { get; set; }

    /// <summary>Gets or sets the proxy application client identifier.</summary>
    public string ProxyClientId { get; set; }

    /// <summary>Gets or sets the proxy application client secret.</summary>
    [Secret]
    public string ProxyClientSecret { get; set; }
}