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

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

namespace Ark.Tools.Auth0;

public sealed class AuthenticationApiClientCachingDecorator : IAuthenticationApiClient, IDisposable
{
    private readonly IAuthenticationApiClient _inner;
    #if NET10_0_OR_GREATER
    [Secret]
    #endif
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
                    new ResultTtl<AccessTokenResponse>(static r => r is not null ? new Ttl(_expiresIn(r)) : new Ttl(TimeSpan.Zero))
                    );

        _userInfoCachePolicy =
                Policy.CacheAsync(
                    _memoryCacheProvider.AsyncFor<UserInfo>(),
                    new ContextualTtl());

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
#if NET10_0_OR_GREATER
    [Secret]
#endif
 string accessToken)
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

    private async Task<AccessTokenResponse> _getToken<TRequest>(
        TRequest request,
        Func<TRequest, string> getKey,
        
#if NET10_0_OR_GREATER
    [NotPersonalData("Token retrieval delegate is executable logic, not redacted data.")]
#endif
 Func<TRequest, CancellationToken, Task<AccessTokenResponse>> getTokenAsync,
        CancellationToken cancellationToken = default)
        where TRequest : notnull
    {
        var key = getKey(request);

        var task = _pendingTasks.GetOrAdd(
            key,
            static (k, state) => state.Policy.ExecuteAsync(
                static context => _executeToken<TRequest>(context),
                new Context(k, new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["state"] = (state.Request, state.GetTokenAsync, state.CancellationToken)
                })
            ),
            (
                Policy: _accessTokenResponseCachePolicy,
                Request: request,
                GetTokenAsync: getTokenAsync,
                CancellationToken: cancellationToken
            )
        ) as Task<AccessTokenResponse>;

        try
        {
            return await task!.ConfigureAwait(false);
        }
        finally
        {
            _pendingTasks.TryRemove(key, out var _);
        }
    }

    private static Task<AccessTokenResponse> _executeToken<TRequest>(Context context)
        where TRequest : notnull
    {
        var state = ((TRequest Request, Func<TRequest, CancellationToken, Task<AccessTokenResponse>> GetTokenAsync, CancellationToken CancellationToken))context["state"];
        return state.GetTokenAsync(state.Request, state.CancellationToken);
    }

    public Task<AccessTokenResponse> GetTokenAsync(AuthorizationCodeTokenRequest request, CancellationToken cancellationToken = default)
    {
        return _getToken(request, _getKey, _inner.GetTokenAsync, cancellationToken);
    }

    private static string _getKey(AuthorizationCodeTokenRequest r)
    {
        return $"AuthorizationCodeTokenRequest{_hashKey(r.ClientId)}{_hashKey(r.Code)}"; // code should be enough, but being on safe side
    }

    public Task<AccessTokenResponse> GetTokenAsync(AuthorizationCodePkceTokenRequest request, CancellationToken cancellationToken = default)
    {
        return _getToken(request, _getKey, _inner.GetTokenAsync, cancellationToken);
    }

    private static string _getKey(AuthorizationCodePkceTokenRequest r)
    {
        return $"AuthorizationCodePkceTokenRequest{_hashKey(r.ClientId)}{_hashKey(r.Code)}{_hashKey(r.CodeVerifier)}";
    }

    public Task<AccessTokenResponse> GetTokenAsync(ClientCredentialsTokenRequest request, CancellationToken cancellationToken = default)
    {
        return _getToken(request, _getKey, _inner.GetTokenAsync, cancellationToken);
    }

    private static string _getKey(ClientCredentialsTokenRequest r)
    {
        return $"ClientCredentialsTokenRequest{_hashKey(r.ClientId)}{_hashKey(r.Audience)}";
    }

    public Task<AccessTokenResponse> GetTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        return _getToken(request, _getKey, _inner.GetTokenAsync, cancellationToken);
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

    public Task<AccessTokenResponse> GetTokenAsync(ResourceOwnerTokenRequest request, CancellationToken cancellationToken = default)
    {
        return _getToken(request, _getKey, _inner.GetTokenAsync, cancellationToken);
    }

    private static string _getKey(ResourceOwnerTokenRequest r)
    {
        return $"ResourceOwnerTokenRequest{_hashKey(r.ClientId)}{_hashKey(r.Username)}{_hashKey(r.Realm)}{_hashKey(r.Audience)}{_hashKey(r.Scope)}";
    }

    public Task<UserInfo> GetUserInfoAsync(
#if NET10_0_OR_GREATER
    [Secret]
#endif
 string accessToken, CancellationToken cancellationToken = default)
    {
        return _userInfoCachePolicy.ExecuteAsync((_, ctk) => _inner.GetUserInfoAsync(accessToken, ctk), new Context(AuthenticationApiClientCachingDecorator._getKey(accessToken), new Dictionary<string, object>(StringComparer.Ordinal)
        {
            { ContextualTtl.TimeSpanKey, _expiresIn(accessToken) }
        }), cancellationToken);
    }

    private static string _getKey(
#if NET10_0_OR_GREATER
    [Secret]
#endif
 string accessToken)
    {
        return $"GetUserInfo{_hashKey(accessToken)}";
    }

    public Task<AccessTokenResponse> GetTokenAsync(PasswordlessEmailTokenRequest request, CancellationToken cancellationToken = default)
    {
        return _getToken(request, _getKey, _inner.GetTokenAsync, cancellationToken);
    }

    private static string _getKey(PasswordlessEmailTokenRequest r)
    {
        return $"PasswordlessEmailTokenRequest{_hashKey(r.ClientId)}{_hashKey(r.Email)}{_hashKey(r.Audience)}{_hashKey(r.Scope)}";
    }

    public Task<AccessTokenResponse> GetTokenAsync(PasswordlessSmsTokenRequest request, CancellationToken cancellationToken = default)
    {
        return _getToken(request, _getKey, _inner.GetTokenAsync, cancellationToken);
    }

    private static string _getKey(PasswordlessSmsTokenRequest r)
    {
        return $"PasswordlessSmsTokenRequest{_hashKey(r.ClientId)}{_hashKey(r.PhoneNumber)}{_hashKey(r.Audience)}{_hashKey(r.Scope)}";
    }

    public Task<AccessTokenResponse> GetTokenAsync(DeviceCodeTokenRequest request, CancellationToken cancellationToken = default)
    {
        return _getToken(request, _getKey, _inner.GetTokenAsync, cancellationToken);
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
#if NET10_0_OR_GREATER
    [Secret]
#endif
 string accessToken, CancellationToken cancellationToken = default)
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