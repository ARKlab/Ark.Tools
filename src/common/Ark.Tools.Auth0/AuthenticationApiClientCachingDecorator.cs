// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Builders;
using Auth0.AuthenticationApi.Models;
using Auth0.AuthenticationApi.Models.Ciba;
using Auth0.AuthenticationApi.Models.Mfa;

using JWT.Algorithms;
using JWT.Builder;
using JWT.Serializers;

using Microsoft.Extensions.Caching.Memory;

using Polly;
using Polly.Caching;
using Polly.Caching.Memory;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Auth0
{
    public sealed class AuthenticationApiClientCachingDecorator : IAuthenticationApiClient, IDisposable
    {
        private readonly IAuthenticationApiClient _inner;
        private readonly AsyncPolicy<AccessTokenResponse> _accessTokenResponseCachePolicy;
        private readonly AsyncPolicy<UserInfo> _userInfoCachePolicy;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly MemoryCacheProvider _memoryCacheProvider;
        private readonly ConcurrentDictionary<string, Task> _pendingTasks = new(StringComparer.Ordinal);


        public AuthenticationApiClientCachingDecorator(IAuthenticationApiClient inner)
        {
            _inner = inner;
            _memoryCacheProvider = new MemoryCacheProvider(_cache);
            _accessTokenResponseCachePolicy =
                    Policy.CacheAsync(
                        _memoryCacheProvider.AsyncFor<AccessTokenResponse>(),
                        new ResultTtl<AccessTokenResponse>(r => r is not null ? new Ttl(_expiresIn(r)) : new Ttl(TimeSpan.Zero))
                        );

            _userInfoCachePolicy =
                    Policy.CacheAsync(
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
#pragma warning disable CS0618 // Type or member is obsolete
            var decode = new JwtBuilder()
                                .DoNotVerifySignature()
                                .WithAlgorithm(new HMACSHA256Algorithm())
                                .WithJsonSerializer(new SystemTextSerializer())
                                .Decode<Token>(accessToken);

#pragma warning restore CS0618 // Type or member is obsolete

            var res = DateTimeOffset.FromUnixTimeSeconds(decode.Exp) - DateTimeOffset.UtcNow;
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

        [Obsolete("GetImpersonationUrlAsync is deprecated")]
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

        private async Task<AccessTokenResponse> _getToken<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : notnull
        {
            var key = (string)_getKey((dynamic)request);

            var task = _pendingTasks.GetOrAdd(
                key,
                k => _accessTokenResponseCachePolicy.ExecuteAsync(
                    ctx => _inner.GetTokenAsync((dynamic)request, cancellationToken),
                    new Context(k)
                )
            ) as Task<AccessTokenResponse>;

            var res = await task!.ConfigureAwait(false);

            _pendingTasks.TryRemove(key, out var _);
            return res;
        }

        public Task<AccessTokenResponse> GetTokenAsync(AuthorizationCodeTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private static string _getKey(AuthorizationCodeTokenRequest r)
        {
            return $"AuthorizationCodeTokenRequest{r.ClientId}{r.Code}"; // code should be enough, but being on safe side
        }

        public Task<AccessTokenResponse> GetTokenAsync(AuthorizationCodePkceTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private static string _getKey(AuthorizationCodePkceTokenRequest r)
        {
            return $"AuthorizationCodePkceTokenRequest{r.ClientId}{r.Code}{r.CodeVerifier}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(ClientCredentialsTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private static string _getKey(ClientCredentialsTokenRequest r)
        {
            return $"ClientCredentialsTokenRequest{r.ClientId}{r.Audience}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private static string _getKey(RefreshTokenRequest r)
        {
            return $"RefreshTokenRequest{r.ClientId}{r.RefreshToken}{r.Audience}{r.Scope}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(ResourceOwnerTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private static string _getKey(ResourceOwnerTokenRequest r)
        {
            return $"ResourceOwnerTokenRequest{r.ClientId}{r.Username}{r.Realm}{r.Audience}{r.Scope}";
        }

        public Task<UserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            return _userInfoCachePolicy.ExecuteAsync((_, ctk) => _inner.GetUserInfoAsync(accessToken, ctk), new Context(AuthenticationApiClientCachingDecorator._getKey(accessToken), new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ContextualTtl.TimeSpanKey, _expiresIn(accessToken) }
            }), cancellationToken);
        }

        private static string _getKey(string accessToken)
        {
            return $"GetUserInfo{accessToken}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(PasswordlessEmailTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private static string _getKey(PasswordlessEmailTokenRequest r)
        {
            return $"PasswordlessEmailTokenRequest{r.ClientId}{r.Email}{r.Audience}{r.Scope}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(PasswordlessSmsTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private static string _getKey(PasswordlessSmsTokenRequest r)
        {
            return $"PasswordlessSmsTokenRequest{r.ClientId}{r.PhoneNumber}{r.Audience}{r.Scope}";
        }

        public Task<AccessTokenResponse> GetTokenAsync(DeviceCodeTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _getToken(request, cancellationToken);
        }

        private static string _getKey(DeviceCodeTokenRequest r)
        {
            return $"DeviceCodeTokenRequest{r.ClientId}{r.DeviceCode}";
        }

        public Task<DeviceCodeResponse> StartDeviceFlowAsync(DeviceCodeRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.StartDeviceFlowAsync(request, cancellationToken);
        }

        public void Dispose()
        {
            ((IDisposable)_cache).Dispose();
            if (_inner is IDisposable disposable) disposable.Dispose();
        }

        public Task RevokeRefreshTokenAsync(RevokeRefreshTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.RevokeRefreshTokenAsync(request, cancellationToken);
        }

        public Task<PushedAuthorizationRequestResponse> PushedAuthorizationRequestAsync(PushedAuthorizationRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.PushedAuthorizationRequestAsync(request, cancellationToken);
        }

        public Task<ClientInitiatedBackchannelAuthorizationResponse> ClientInitiatedBackchannelAuthorization(ClientInitiatedBackchannelAuthorizationRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.ClientInitiatedBackchannelAuthorization(request, cancellationToken);
        }

        public Task<ClientInitiatedBackchannelAuthorizationTokenResponse> GetTokenAsync(ClientInitiatedBackchannelAuthorizationTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.GetTokenAsync(request, cancellationToken);
        }

        public Task<AssociateMfaAuthenticatorResponse> AssociateMfaAuthenticatorAsync(AssociateMfaAuthenticatorRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.AssociateMfaAuthenticatorAsync(request, cancellationToken);
        }

        public Task<IList<Authenticator>> ListMfaAuthenticatorsAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            return _inner.ListMfaAuthenticatorsAsync(accessToken, cancellationToken);
        }

        public Task DeleteMfaAuthenticatorAsync(DeleteMfaAuthenticatorRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.DeleteMfaAuthenticatorAsync(request, cancellationToken);
        }

        public Task<MfaOobTokenResponse> GetTokenAsync(MfaOobTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.GetTokenAsync(request, cancellationToken);
        }

        public Task<MfaOtpTokenResponse> GetTokenAsync(MfaOtpTokenRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.GetTokenAsync(request, cancellationToken);
        }

        public Task<MfaRecoveryCodeResponse> GetTokenAsync(MfaRecoveryCodeRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.GetTokenAsync(request, cancellationToken);
        }

        public Task<MfaChallengeResponse> MfaChallenge(MfaChallengeRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.MfaChallenge(request, cancellationToken);
        }
        #endregion
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection")]
    sealed record Token
    {
        [JsonPropertyName("exp")]
        public long Exp { get; set; }
    }
}
