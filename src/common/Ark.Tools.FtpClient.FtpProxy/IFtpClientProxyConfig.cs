// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Compliance;

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

    /// <summary>
    /// Gets a value indicating whether the TLS certificate of the FtpProxy service is accepted without validation.
    /// Defaults to <see langword="false"/>, so the certificate is validated against the trusted roots.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WARNING: setting this to <see langword="true"/> disables TLS server certificate validation, so the bearer token
    /// and the FTP credentials sent to the proxy are exposed to anyone able to intercept the connection.
    /// </para>
    /// <para>
    /// Only use it for a development FtpProxy that uses a self-signed certificate. Never enable it in production.
    /// </para>
    /// </remarks>
    [NotPersonalData("Boolean TLS validation switch, holds no certificate or personal data.")]
    bool AcceptAnyServerCertificate => false;
}