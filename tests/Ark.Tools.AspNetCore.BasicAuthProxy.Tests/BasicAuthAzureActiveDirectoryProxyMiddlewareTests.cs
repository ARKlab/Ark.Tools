// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.BasicAuthAzureActiveDirectoryProxy;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using System.Net;

namespace Ark.Tools.AspNetCore.BasicAuthProxy.Tests;

[TestClass]
public class BasicAuthAzureActiveDirectoryProxyMiddlewareTests
{
    [TestMethod]
    public async Task Invoke_WrongPassword_CallsIdentityProviderOnce()
    {
        using var handler = new StubHandler(HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.BadRequest);

        await _invokeAsync(handler);

        handler.Calls.Should().Be(1);
    }

    [TestMethod]
    public async Task Invoke_TransientFailure_IsRetried()
    {
        using var handler = new StubHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);

        await _invokeAsync(handler);

        handler.Calls.Should().Be(2);
    }

    private static async Task _invokeAsync(StubHandler handler)
    {
        var config = new BasicAuthAzureActiveDirectoryProxyConfig("tenant", "resource", "client", "proxy-secret");
        using var sut = new BasicAuthAzureActiveDirectoryProxyMiddleware(static _ => Task.CompletedTask, config, NullLogger<BasicAuthAzureActiveDirectoryProxyMiddleware>.Instance, handler);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("Customer A:wrong"));

        await sut.Invoke(context).ConfigureAwait(false);
    }

    private sealed class StubHandler(params HttpStatusCode[] responses) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var status = responses[Calls++];
            await Task.Yield();
            return new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }
    }
}
