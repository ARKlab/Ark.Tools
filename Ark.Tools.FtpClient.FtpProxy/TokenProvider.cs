﻿// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Auth0;

using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;

using Microsoft.IdentityModel.Clients.ActiveDirectory;

using Polly;

using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.FtpClient.FtpProxy
{

    internal class TokenProvider
    {
        private readonly AuthenticationContext _adal;
        private readonly IAuthenticationApiClient _auth0;
        private readonly IFtpClientProxyConfig _config;

        public TokenProvider(IFtpClientProxyConfig config)
        {
            _config = config;

            if (_config.UseAuth0)
                _auth0 = new AuthenticationApiClientCachingDecorator(new AuthenticationApiClient(_config.TenantID));
            else
                _adal = new AuthenticationContext("https://login.microsoftonline.com/" + this._config.TenantID);
        }

        public Task<string> GetToken(CancellationToken ctk = default)
        {
            if (_config.UseAuth0)
                return _getAuth0AccessToken(ctk);
            else
                return _getAdalAccessToken(ctk);
        }

        private async Task<string> _getAuth0AccessToken(CancellationToken ctk = default(CancellationToken))
        {

            try
            {
                var result = await Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(3, r => TimeSpan.FromSeconds(3))
                    .ExecuteAsync(() => _auth0.GetTokenAsync(new ClientCredentialsTokenRequest
                    {
                        Audience = _config.ApiIdentifier,
                        ClientId = _config.ClientID,
                        ClientSecret = _config.ClientKey
                    }))
                    .ConfigureAwait(false);

                return result.AccessToken;
            }
            catch (Exception ex)
            {
                throw new AuthenticationException("Failed to acquire token, check credentials", ex);
            }
        }

        private async Task<string> _getAdalAccessToken(CancellationToken ctk = default(CancellationToken))
        {
            AuthenticationResult result = null;
            try
            {
                result = await Policy
                    .Handle<AdalException>(ex => ex.ErrorCode == "temporarily_unavailable")
                    .WaitAndRetryAsync(3, r => TimeSpan.FromSeconds(3))
                    .ExecuteAsync(c => _adal.AcquireTokenAsync(_config.ApiIdentifier, new ClientCredential(this._config.ClientID, this._config.ClientKey)), ctk, false)
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
