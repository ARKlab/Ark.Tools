// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Auth0;

using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;

using Microsoft.Identity.Client;

using Polly;

using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.FtpClient.FtpProxy
{

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    internal sealed class TokenProvider
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private readonly IConfidentialClientApplication? _adal;
        private readonly IAuthenticationApiClient? _auth0;
        private readonly IFtpClientProxyConfig _config;

        public TokenProvider(IFtpClientProxyConfig config)
        {
            _config = config;

            if (_config.UseAuth0)
#pragma warning disable CA2000 // Dispose objects before losing scope
                _auth0 = new AuthenticationApiClientCachingDecorator(new AuthenticationApiClient(_config.TenantID));
#pragma warning restore CA2000 // Dispose objects before losing scope
            else
                _adal = ConfidentialClientApplicationBuilder.Create(_config.ClientID)
                                          .WithClientSecret(_config.ClientKey)
                                          .WithAuthority(new Uri("https://login.microsoftonline.com/" + this._config.TenantID))
                                          .Build();
        }

        public Task<string> GetToken(CancellationToken ctk = default)
        {
            if (_config.UseAuth0)
                return _getAuth0AccessToken(ctk);
            else
                return _getAdalAccessToken(ctk);
        }

        private async Task<string> _getAuth0AccessToken(CancellationToken ctk = default)
        {
            var auth0 = _auth0 ?? throw new InvalidOperationException("Auth0 client is not initialized");
            try
            {
                var result = await Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(3, r => TimeSpan.FromSeconds(3))
                    .ExecuteAsync((ct) => auth0.GetTokenAsync(new ClientCredentialsTokenRequest
                    {
                        Audience = _config.ApiIdentifier,
                        ClientId = _config.ClientID,
                        ClientSecret = _config.ClientKey
                    }, ct),ctk)
.ConfigureAwait(false);

                return result.AccessToken;
            }
            catch (Exception ex)
            {
                throw new AuthenticationException("Failed to acquire token, check credentials", ex);
            }
        }

        private async Task<string> _getAdalAccessToken(CancellationToken ctk = default)
        {
            AuthenticationResult? result = null;
            var adal = _adal ?? throw new InvalidOperationException("ADAL client is not initialized");
            try
            {
                result = await Policy
                    .Handle<MsalException>(ex => ex.IsRetryable)
                    .WaitAndRetryAsync(3, r => TimeSpan.FromSeconds(3))
                    .ExecuteAsync(c => adal.AcquireTokenForClient([_config.ApiIdentifier + "/.default"]).ExecuteAsync(c), ctk, false)
.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new AuthenticationException("Failed to acquire token, check credentials", ex);
            }

            if (result == null)
                throw new AuthenticationException("Failed to acquire token, check credentials");

            return result.AccessToken;
        }
    }
}
