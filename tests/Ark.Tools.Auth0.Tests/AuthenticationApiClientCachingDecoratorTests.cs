// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;

using AwesomeAssertions;

using Moq;

namespace Ark.Tools.Auth0.Tests;

[TestClass]
public class AuthenticationApiClientCachingDecoratorTests
{
    private static readonly ClientCredentialsTokenRequest _request = new()
    {
        ClientId = "client",
        ClientSecret = "secret",
        Audience = "audience",
    };

    [TestMethod]
    public async Task GetTokenAsync_ValidToken_IsCached()
    {
        var inner = new Mock<IAuthenticationApiClient>();
        inner.Setup(static x => x.GetTokenAsync(It.IsAny<ClientCredentialsTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(static () => _tokenResponse(TimeSpan.FromHours(1)));
        using var sut = new AuthenticationApiClientCachingDecorator(inner.Object);

        var first = await sut.GetTokenAsync(_request);
        var second = await sut.GetTokenAsync(_request);

        second.Should().BeSameAs(first);
        inner.Verify(static x => x.GetTokenAsync(It.IsAny<ClientCredentialsTokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetTokenAsync_ExpiredToken_IsNotCached()
    {
        var inner = new Mock<IAuthenticationApiClient>();
        inner.Setup(static x => x.GetTokenAsync(It.IsAny<ClientCredentialsTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(static () => _tokenResponse(TimeSpan.FromHours(-1)));
        using var sut = new AuthenticationApiClientCachingDecorator(inner.Object);

        await sut.GetTokenAsync(_request);
        await sut.GetTokenAsync(_request);

        inner.Verify(static x => x.GetTokenAsync(It.IsAny<ClientCredentialsTokenRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task GetTokenAsync_ConcurrentSameKey_CallsInnerOnce()
    {
        var gate = new TaskCompletionSource<AccessTokenResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new Mock<IAuthenticationApiClient>();
        inner.Setup(static x => x.GetTokenAsync(It.IsAny<ClientCredentialsTokenRequest>(), It.IsAny<CancellationToken>()))
            .Returns(gate.Task);
        using var sut = new AuthenticationApiClientCachingDecorator(inner.Object);

        var calls = Enumerable.Range(0, 5).Select(_ => sut.GetTokenAsync(_request)).ToList();
        gate.SetResult(_tokenResponse(TimeSpan.FromHours(1)));
        var results = await Task.WhenAll(calls);

        results.Should().AllSatisfy(r => r.Should().BeSameAs(results[0]));
        inner.Verify(static x => x.GetTokenAsync(It.IsAny<ClientCredentialsTokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetUserInfoAsync_ValidToken_IsCached()
    {
        var accessToken = _jwt(TimeSpan.FromHours(1));
        var inner = new Mock<IAuthenticationApiClient>();
        inner.Setup(x => x.GetUserInfoAsync(accessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(static () => new UserInfo());
        using var sut = new AuthenticationApiClientCachingDecorator(inner.Object);

        var first = await sut.GetUserInfoAsync(accessToken);
        var second = await sut.GetUserInfoAsync(accessToken);

        second.Should().BeSameAs(first);
        inner.Verify(x => x.GetUserInfoAsync(accessToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AccessTokenResponse _tokenResponse(TimeSpan expiresIn)
    {
        return new AccessTokenResponse
        {
            AccessToken = _jwt(expiresIn),
            ExpiresIn = (int)expiresIn.TotalSeconds,
        };
    }

    private static string _jwt(TimeSpan expiresIn)
    {
        var exp = DateTimeOffset.UtcNow.Add(expiresIn).ToUnixTimeSeconds();
        return $"{_base64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")}.{_base64Url($"{{\"exp\":{exp}}}")}.{_base64Url("signature")}";
    }

    private static string _base64Url(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
