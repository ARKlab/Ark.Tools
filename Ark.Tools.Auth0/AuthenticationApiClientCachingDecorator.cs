// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Builders;
using Auth0.AuthenticationApi.Models;
using Auth0.Core.Http;
using JWT.Builder;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.Caching;
using Polly.Caching.Memory;

namespace Ark.Tools.Auth0
{
    public sealed class AuthenticationApiClientCachingDecorator : IAuthenticationApiClient
    {
        private IAuthenticationApiClient _inner;
        private readonly CachePolicy<AccessTokenResponse> _accessTokenResponseCachePolicy;
        private readonly CachePolicy<UserInfo> _userInfoCachePolicy;
        private readonly MemoryCacheProvider _memoryCacheProvider = new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions()));
        private readonly ConcurrentDictionary<string, Task> _pendingTasks = new ConcurrentDictionary<string, Task>();

        public AuthenticationApiClientCachingDecorator(IAuthenticationApiClient inner)
        {
            _inner = inner;
            _accessTokenResponseCachePolicy = Policy.CacheAsync(
                _memoryCacheProvider.AsyncFor<AccessTokenResponse>(),
                new ResultTtl<AccessTokenResponse>(r => new Ttl(_expiresIn(r), false)));

            _userInfoCachePolicy = Policy.CacheAsync(
                _memoryCacheProvider.AsyncFor<UserInfo>(),
                new ContextualTtl());
        }

        private static TimeSpan _expiresIn(AccessTokenResponse r)
        {
            var expAccessToken = _expiresIn(r.AccessToken);
            var expIdToken = TimeSpan.FromSeconds(r.ExpiresIn);

            return new[] { expAccessToken, expIdToken }.Min();
        }

        private static TimeSpan _expiresIn(string accessToken)
        {
            var decode = new JwtBuilder()
                                .DoNotVerifySignature()
                                .Decode<IDictionary<string, object>>(accessToken);

            var res = DateTimeOffset.FromUnixTimeSeconds((long)decode["exp"]) - DateTimeOffset.UtcNow;
            return res;
        }

        #region Passthrough
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

        public Task<string> ChangePasswordAsync(ChangePasswordRequest request)
        {
            return _inner.ChangePasswordAsync(request);
        }

        public Task<Uri> GetImpersonationUrlAsync(ImpersonationRequest request)
        {
            return _inner.GetImpersonationUrlAsync(request);
        }

        public ApiInfo GetLastApiInfo()
        {
            return _inner.GetLastApiInfo();
        }

        public Task<string> GetSamlMetadataAsync(string clientId)
        {
            return _inner.GetSamlMetadataAsync(clientId);
        }

        public Task<string> GetWsFedMetadataAsync()
        {
            return _inner.GetWsFedMetadataAsync();
        }

        public Task<SignupUserResponse> SignupUserAsync(SignupUserRequest request)
        {
            return _inner.SignupUserAsync(request);
        }

        public Task<PasswordlessEmailResponse> StartPasswordlessEmailFlowAsync(PasswordlessEmailRequest request)
        {
            return _inner.StartPasswordlessEmailFlowAsync(request);
        }

        public Task<PasswordlessSmsResponse> StartPasswordlessSmsFlowAsync(PasswordlessSmsRequest request)
        {
            return _inner.StartPasswordlessSmsFlowAsync(request);
        }

        public Task UnlinkUserAsync(UnlinkUserRequest request)
        {
            return _inner.UnlinkUserAsync(request);
        }

        #endregion

        #region Caching

        private async Task<AccessTokenResponse> _getToken<TRequest>(TRequest request)
        {
            var key = (string)_getKey((dynamic)request);
            var task = _pendingTasks.GetOrAdd(
                key, 
                k => _accessTokenResponseCachePolicy.ExecuteAsync(
                    ctx => _inner.GetTokenAsync((dynamic)request), 
                    new Context(k)
                )
            ) as Task<AccessTokenResponse>;

            var res = await task;

            _pendingTasks.TryRemove(key, out var _);

            return res;
        }
        
        public Task<AccessTokenResponse> GetTokenAsync(AuthorizationCodeTokenRequest request)
        {
            return _getToken(request);
        }

        private string _getKey(AuthorizationCodeTokenRequest r)
        {
            return $"AuthorizationCodeTokenRequest{r.ClientId}{r.Code}"; // code should be enough, but being on safe side
        }

        public Task<AccessTokenResponse> GetTokenAsync(AuthorizationCodePkceTokenRequest request)
        {
            return _getToken(request);
        }

        private string _getKey(AuthorizationCodePkceTokenRequest r)
        {
            return $"AuthorizationCodePkceTokenRequest{r.ClientId}{r.Code}{r.CodeVerifier}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(ClientCredentialsTokenRequest request)
        {
            return _getToken(request);
        }

        private string _getKey(ClientCredentialsTokenRequest r)
        {
            return $"ClientCredentialsTokenRequest{r.ClientId}{r.Audience}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(RefreshTokenRequest request)
        {
            return _getToken(request);
        }

        private string _getKey(RefreshTokenRequest r)
        {
            return $"RefreshTokenRequest{r.ClientId}{r.RefreshToken}{r.Audience}{r.Scope}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(ResourceOwnerTokenRequest request)
        {
            return _getToken(request);
        }

        private string _getKey(ResourceOwnerTokenRequest r)
        {
            return $"ResourceOwnerTokenRequest{r.ClientId}{r.Username}{r.Realm}{r.Audience}{r.Scope}";
        }

        public Task<UserInfo> GetUserInfoAsync(string accessToken)
        {
            return _userInfoCachePolicy.ExecuteAsync(ctx => _inner.GetUserInfoAsync(accessToken), new Context(_getKey(accessToken), new Dictionary<string, object>()
            {
                { ContextualTtl.TimeSpanKey, _expiresIn(accessToken) }
            }));
        }

        private string _getKey(string accessToken)
        {
            return $"GetUserInfo{accessToken}";
        }
        #endregion
    }
}
