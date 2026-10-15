// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.BasicAuthAuth0Proxy;

using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Auth0.Core.Exceptions;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using System.Net;

namespace Ark.Tools.AspNetCore.BasicAuthProxy.Tests;

[TestClass]
public class BasicAuthAuth0ProxyMiddlewareTests
{
    [TestMethod]
    public async Task Invoke_WrongPassword_CallsIdentityProviderOnce()
    {
        var auth0 = new Mock<IAuthenticationApiClient>();
        auth0.Setup(static x => x.GetTokenAsync(It.IsAny<ResourceOwnerTokenRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ErrorApiException(HttpStatusCode.Forbidden));

        var context = await _invokeAsync(auth0.Object);

        auth0.Verify(static x => x.GetTokenAsync(It.IsAny<ResourceOwnerTokenRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        context.Request.Headers.Authorization.ToString().Should().StartWith("Basic ");
    }

    [TestMethod]
    public async Task Invoke_TransientFailure_IsRetried()
    {
        var jwt = _validJwt();
        var auth0 = new Mock<IAuthenticationApiClient>();
        auth0.SetupSequence(static x => x.GetTokenAsync(It.IsAny<ResourceOwnerTokenRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ErrorApiException(HttpStatusCode.ServiceUnavailable))
            .ReturnsAsync(new AccessTokenResponse { AccessToken = jwt, ExpiresIn = 3600 });

        var context = await _invokeAsync(auth0.Object);

        auth0.Verify(static x => x.GetTokenAsync(It.IsAny<ResourceOwnerTokenRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        context.Request.Headers.Authorization.ToString().Should().Be("Bearer " + jwt);
    }

    private static string _validJwt()
    {
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        return $"{_base64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")}.{_base64Url($"{{\"exp\":{exp}}}")}.{_base64Url("signature")}";
    }

    private static string _base64Url(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static async Task<HttpContext> _invokeAsync(IAuthenticationApiClient auth0)
    {
        var config = new BasicAuthAuth0ProxyConfig("audience", "client", "example.auth0.com", "proxy-secret");
        using var sut = new BasicAuthAuth0ProxyMiddleware(static _ => Task.CompletedTask, config, NullLogger<BasicAuthAuth0ProxyMiddleware>.Instance, auth0);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("Customer A:wrong"));

        await sut.Invoke(context).ConfigureAwait(false);

        return context;
    }
}
