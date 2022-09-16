// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Builders;
using Auth0.AuthenticationApi.Models;
using Auth0.Core.Http;
using JWT.Algorithms;
using JWT.Builder;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.Caching;
using Polly.Caching.Memory;
using Polly.Contrib.DuplicateRequestCollapser;

namespace Ark.Tools.Auth0
{
    public sealed class AuthenticationApiClientCachingDecorator : IAuthenticationApiClient
    {
        private IAuthenticationApiClient _inner;
        private readonly AsyncPolicy<AccessTokenResponse> _accessTokenResponseCachePolicy;
        private readonly AsyncPolicy<UserInfo> _userInfoCachePolicy;
        private readonly MemoryCacheProvider _memoryCacheProvider = new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions()));

        public AuthenticationApiClientCachingDecorator(IAuthenticationApiClient inner)
        {
            _inner = inner;
            _accessTokenResponseCachePolicy = AsyncRequestCollapserPolicy.Create()
                .WrapAsync(
                    Policy.CacheAsync(
                        _memoryCacheProvider.AsyncFor<AccessTokenResponse>(),
                        new ResultTtl<AccessTokenResponse>(r => new Ttl(_expiresIn(r), false)))
                );

            _userInfoCachePolicy = AsyncRequestCollapserPolicy.Create()
                .WrapAsync(
                    Policy.CacheAsync(
                        _memoryCacheProvider.AsyncFor<UserInfo>(),
                        new ContextualTtl())
                );
        }

        private static TimeSpan _expiresIn(AccessTokenResponse r)
        {
            var expAccessToken = _expiresIn(r.AccessToken);
            var expIdToken = TimeSpan.FromSeconds(r.ExpiresIn);

            return new[] { expAccessToken, expIdToken }.Min();
        }

        private static TimeSpan _expiresIn(string accessToken)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var decode = new JwtBuilder()
                                .DoNotVerifySignature()
                                .WithAlgorithm(new HMACSHA256Algorithm())
                                .Decode<IDictionary<string, object>>(accessToken);
#pragma warning restore CS0618 // Type or member is obsolete

            var res = DateTimeOffset.FromUnixTimeSeconds((long)decode["exp"]) - DateTimeOffset.UtcNow;
            return res;
        }

        #region Passthrough

        public Uri BaseUri => _inner.BaseUri;

        public AuthorizationUrlBuilder BuildAuthorizationUrl()
        {
            return _inner.BuildAuthorizationUrl();
        }

        public LogoutUrlBuilder BuildLogoutUrl()
        {
            return _inner.BuildLogoutUrl();
        }

        public SamlUrlBuilder BuildSamlUrl(string client)
        {
            return _inner.BuildSamlUrl(client);
        }

        public WsFedUrlBuilder BuildWsFedUrl()
        {
            return _inner.BuildWsFedUrl();
        }

        public Task<string> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.ChangePasswordAsync(request, cancellationToken);
        }

        public Task<Uri> GetImpersonationUrlAsync(ImpersonationRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.GetImpersonationUrlAsync(request, cancellationToken);
        }

        public Task<SignupUserResponse> SignupUserAsync(SignupUserRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.SignupUserAsync(request, cancellationToken);
        }

        public Task<PasswordlessEmailResponse> StartPasswordlessEmailFlowAsync(PasswordlessEmailRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.StartPasswordlessEmailFlowAsync(request, cancellationToken);
        }

        public Task<PasswordlessSmsResponse> StartPasswordlessSmsFlowAsync(PasswordlessSmsRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.StartPasswordlessSmsFlowAsync(request, cancellationToken);
        }

        #endregion

        #region Caching

        private async Task<AccessTokenResponse> _getToken<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        {
            var key = (string)_getKey((dynamic)request);

            var res = await _accessTokenResponseCachePolicy.ExecuteAsync(
                    (_, ctk) => _inner.GetTokenAsync((dynamic)request, ctk),
                    new Context(key), cancellationToken);

            return res;
        }
        
        public Task<AccessTokenResponse> GetTokenAsync(AuthorizationCodeTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private string _getKey(AuthorizationCodeTokenRequest r)
        {
            return $"AuthorizationCodeTokenRequest{r.ClientId}{r.Code}"; // code should be enough, but being on safe side
        }

        public Task<AccessTokenResponse> GetTokenAsync(AuthorizationCodePkceTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private string _getKey(AuthorizationCodePkceTokenRequest r)
        {
            return $"AuthorizationCodePkceTokenRequest{r.ClientId}{r.Code}{r.CodeVerifier}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(ClientCredentialsTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private string _getKey(ClientCredentialsTokenRequest r)
        {
            return $"ClientCredentialsTokenRequest{r.ClientId}{r.Audience}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private string _getKey(RefreshTokenRequest r)
        {
            return $"RefreshTokenRequest{r.ClientId}{r.RefreshToken}{r.Audience}{r.Scope}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(ResourceOwnerTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private string _getKey(ResourceOwnerTokenRequest r)
        {
            return $"ResourceOwnerTokenRequest{r.ClientId}{r.Username}{r.Realm}{r.Audience}{r.Scope}";
        }

        public Task<UserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            return _userInfoCachePolicy.ExecuteAsync((_,ctk) => _inner.GetUserInfoAsync(accessToken, ctk), new Context(_getKey(accessToken), new Dictionary<string, object>()
            {
                { ContextualTtl.TimeSpanKey, _expiresIn(accessToken) }
            }), cancellationToken);
        }

        private string _getKey(string accessToken)
        {
            return $"GetUserInfo{accessToken}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(PasswordlessEmailTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private string _getKey(PasswordlessEmailTokenRequest r)
        {
            return $"PasswordlessEmailTokenRequest{r.ClientId}{r.Email}{r.Audience}{r.Scope}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(PasswordlessSmsTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private string _getKey(PasswordlessSmsTokenRequest r)
        {
            return $"PasswordlessSmsTokenRequest{r.ClientId}{r.PhoneNumber}{r.Audience}{r.Scope}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(DeviceCodeTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private string _getKey(DeviceCodeTokenRequest r)
        {
            return $"DeviceCodeTokenRequest{r.ClientId}{r.DeviceCode}";
        }

        public Task<DeviceCodeResponse> StartDeviceFlowAsync(DeviceCodeRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.StartDeviceFlowAsync(request, cancellationToken);
        }
        #endregion
    }
}
