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

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

using Ark.Tools.Compliance;

namespace Ark.Tools.Auth0;

public sealed class AuthenticationApiClientCachingDecorator : IAuthenticationApiClient, IDisposable
{
    private readonly IAuthenticationApiClient _inner;
    [Secret]
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ConcurrentDictionary<string, Lazy<Task<AccessTokenResponse>>> _pendingTasks = new(StringComparer.Ordinal);


    public AuthenticationApiClientCachingDecorator(IAuthenticationApiClient inner)
    {
        _inner = inner;
    }

    private async Task<T> _getOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, Func<T, TimeSpan> ttl, CancellationToken cancellationToken)
        where T : class
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        var result = await factory(cancellationToken).ConfigureAwait(false);

        if (result is not null)
        {
            var expiration = ttl(result);
            if (expiration > TimeSpan.Zero)
                _cache.Set(key, result, expiration);
        }

        return result!;
    }

    private static string _hashKey(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }

    private static TimeSpan _expiresIn(AccessTokenResponse r)
    {
        var expAccessToken = _expiresIn(r.AccessToken);
        var expIdToken = TimeSpan.FromSeconds(r.ExpiresIn);

        return new[] { expAccessToken, expIdToken }.Min();
    }

    private static TimeSpan _expiresIn(
        [Secret] string accessToken)
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

    public async Task<string> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.ChangePasswordAsync(request, cancellationToken).ConfigureAwait(false);
    }

    [Obsolete("GetImpersonationUrlAsync is deprecated")]
    public async Task<Uri> GetImpersonationUrlAsync(ImpersonationRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.GetImpersonationUrlAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SignupUserResponse> SignupUserAsync(SignupUserRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.SignupUserAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PasswordlessEmailResponse> StartPasswordlessEmailFlowAsync(PasswordlessEmailRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.StartPasswordlessEmailFlowAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PasswordlessSmsResponse> StartPasswordlessSmsFlowAsync(PasswordlessSmsRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.StartPasswordlessSmsFlowAsync(request, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Caching

    private async Task<AccessTokenResponse> _getTokenAsync<TRequest>(
        TRequest request,
        Func<TRequest, string> getKey,
        [NotPersonalData("Token retrieval delegate is executable logic, not redacted data.")] Func<TRequest, CancellationToken, Task<AccessTokenResponse>> getTokenAsync,
        CancellationToken cancellationToken = default)
        where TRequest : notnull
    {
        var key = getKey(request);

        // Lazy: GetOrAdd may run the factory more than once on concurrent misses, but only the stored Lazy is ever started.
        var pending = _pendingTasks.GetOrAdd(
            key,
            static (k, state) => new Lazy<Task<AccessTokenResponse>>(async () => await state.Self._getOrCreateAsync(
                k,
                ct => state.GetTokenAsync(state.Request, ct),
                _expiresIn,
                state.CancellationToken).ConfigureAwait(false)),
            (
                Self: this,
                Request: request,
                GetTokenAsync: getTokenAsync,
                CancellationToken: cancellationToken
            ));

        try
        {
            return await pending.Value.ConfigureAwait(false);
        }
        finally
        {
            _pendingTasks.TryRemove(KeyValuePair.Create(key, pending));
        }
    }

    public async Task<AccessTokenResponse> GetTokenAsync(AuthorizationCodeTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _getTokenAsync(request, _getKey, _inner.GetTokenAsync, cancellationToken).ConfigureAwait(false);
    }

    private static string _getKey(AuthorizationCodeTokenRequest r)
    {
        return $"AuthorizationCodeTokenRequest{_hashKey(r.ClientId)}{_hashKey(r.Code)}"; // code should be enough, but being on safe side
    }

    public async Task<AccessTokenResponse> GetTokenAsync(AuthorizationCodePkceTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _getTokenAsync(request, _getKey, _inner.GetTokenAsync, cancellationToken).ConfigureAwait(false);
    }

    private static string _getKey(AuthorizationCodePkceTokenRequest r)
    {
        return $"AuthorizationCodePkceTokenRequest{_hashKey(r.ClientId)}{_hashKey(r.Code)}{_hashKey(r.CodeVerifier)}";
    }

    public async Task<AccessTokenResponse> GetTokenAsync(ClientCredentialsTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _getTokenAsync(request, _getKey, _inner.GetTokenAsync, cancellationToken).ConfigureAwait(false);
    }

    private static string _getKey(ClientCredentialsTokenRequest r)
    {
        return $"ClientCredentialsTokenRequest{_hashKey(r.ClientId)}{_hashKey(r.Audience)}";
    }

    public async Task<AccessTokenResponse> GetTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _getTokenAsync(request, _getKey, _inner.GetTokenAsync, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Exchanges a token using the OAuth 2.0 Token Exchange grant.
    /// </summary>
    /// <param name="request">The token exchange request.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The requested access token response.</returns>
    public async Task<AccessTokenResponse> GetTokenAsync(TokenExchangeTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.GetTokenAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Exchanges an Auth0 token for an access token issued by a federated connection.
    /// </summary>
    /// <param name="request">The federated connection access token request.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The requested access token response.</returns>
    public async Task<AccessTokenResponse> GetTokenAsync(FederatedConnectionAccessTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.GetTokenAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets an access token using the OAuth 2.0 on-behalf-of token flow.
    /// </summary>
    /// <param name="request">The on-behalf-of token request.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The on-behalf-of token response.</returns>
    public async Task<OnBehalfOfTokenResponse> GetTokenOnBehalfOfAsync(OnBehalfOfTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.GetTokenOnBehalfOfAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static string _getKey(RefreshTokenRequest r)
    {
        return $"RefreshTokenRequest{_hashKey(r.ClientId)}{_hashKey(r.RefreshToken)}{_hashKey(r.Audience)}{_hashKey(r.Scope)}";
    }

    public async Task<AccessTokenResponse> GetTokenAsync(ResourceOwnerTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _getTokenAsync(request, _getKey, _inner.GetTokenAsync, cancellationToken).ConfigureAwait(false);
    }

    private static string _getKey(ResourceOwnerTokenRequest r)
    {
        return $"ResourceOwnerTokenRequest{_hashKey(r.ClientId)}{_hashKey(r.Username)}{_hashKey(r.Realm)}{_hashKey(r.Audience)}{_hashKey(r.Scope)}";
    }

    public async Task<UserInfo> GetUserInfoAsync(
        [Secret] string accessToken, CancellationToken cancellationToken = default)
    {
        var expiresIn = _expiresIn(accessToken);
        return await _getOrCreateAsync(_getKey(accessToken), ct => _inner.GetUserInfoAsync(accessToken, ct), _ => expiresIn, cancellationToken).ConfigureAwait(false);
    }

    private static string _getKey(
        [Secret] string accessToken)
    {
        return $"GetUserInfo{_hashKey(accessToken)}";
    }

    public async Task<AccessTokenResponse> GetTokenAsync(PasswordlessEmailTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _getTokenAsync(request, _getKey, _inner.GetTokenAsync, cancellationToken).ConfigureAwait(false);
    }

    private static string _getKey(PasswordlessEmailTokenRequest r)
    {
        return $"PasswordlessEmailTokenRequest{_hashKey(r.ClientId)}{_hashKey(r.Email)}{_hashKey(r.Audience)}{_hashKey(r.Scope)}";
    }

    public async Task<AccessTokenResponse> GetTokenAsync(PasswordlessSmsTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _getTokenAsync(request, _getKey, _inner.GetTokenAsync, cancellationToken).ConfigureAwait(false);
    }

    private static string _getKey(PasswordlessSmsTokenRequest r)
    {
        return $"PasswordlessSmsTokenRequest{_hashKey(r.ClientId)}{_hashKey(r.PhoneNumber)}{_hashKey(r.Audience)}{_hashKey(r.Scope)}";
    }

    public async Task<AccessTokenResponse> GetTokenAsync(DeviceCodeTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _getTokenAsync(request, _getKey, _inner.GetTokenAsync, cancellationToken).ConfigureAwait(false);
    }

    private static string _getKey(DeviceCodeTokenRequest r)
    {
        return $"DeviceCodeTokenRequest{_hashKey(r.ClientId)}{_hashKey(r.DeviceCode)}";
    }

    public async Task<DeviceCodeResponse> StartDeviceFlowAsync(DeviceCodeRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.StartDeviceFlowAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        ((IDisposable)_cache).Dispose();
        _inner.Dispose();
    }

    public async Task RevokeRefreshTokenAsync(RevokeRefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        await _inner.RevokeRefreshTokenAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PushedAuthorizationRequestResponse> PushedAuthorizationRequestAsync(PushedAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.PushedAuthorizationRequestAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClientInitiatedBackchannelAuthorizationResponse> ClientInitiatedBackchannelAuthorization(ClientInitiatedBackchannelAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.ClientInitiatedBackchannelAuthorization(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClientInitiatedBackchannelAuthorizationTokenResponse> GetTokenAsync(ClientInitiatedBackchannelAuthorizationTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.GetTokenAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssociateMfaAuthenticatorResponse> AssociateMfaAuthenticatorAsync(AssociateMfaAuthenticatorRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.AssociateMfaAuthenticatorAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IList<Authenticator>> ListMfaAuthenticatorsAsync(
        [Secret] string accessToken, CancellationToken cancellationToken = default)
    {
        return await _inner.ListMfaAuthenticatorsAsync(accessToken, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteMfaAuthenticatorAsync(DeleteMfaAuthenticatorRequest request, CancellationToken cancellationToken = default)
    {
        await _inner.DeleteMfaAuthenticatorAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MfaOobTokenResponse> GetTokenAsync(MfaOobTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.GetTokenAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MfaOtpTokenResponse> GetTokenAsync(MfaOtpTokenRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.GetTokenAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MfaRecoveryCodeResponse> GetTokenAsync(MfaRecoveryCodeRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.GetTokenAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MfaChallengeResponse> MfaChallenge(MfaChallengeRequest request, CancellationToken cancellationToken = default)
    {
        return await _inner.MfaChallenge(request, cancellationToken).ConfigureAwait(false);
    }
    #endregion
}

[UnconditionalSuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection")]
sealed record Token
{
    [JsonPropertyName("exp")]
    public long Exp { get; set; }
}