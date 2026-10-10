// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated Minimal API endpoints run endpoint filters as ASP.NET Core Minimal API does.</summary>
[TestClass]
public sealed class MinimalApiEndpointFilterTests
{
    /// <summary>Verifies a group filter sees the bound arguments and wraps the handler result.</summary>
    [TestMethod]
    public async Task RunsGroupFiltersWithTheBoundArguments()
    {
        var arguments = new ConcurrentQueue<object?[]>();
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync(group => group.AddEndpointFilter(async (context, next) =>
        {
            arguments.Enqueue(context.Arguments.ToArray());
            context.HttpContext.Response.Headers["X-Filtered"] = "true";
            return await next(context).ConfigureAwait(false);
        })).ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/hosting/requests/42?Filter=query",
            new HostingRequest { Value = "body-value" },
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Filtered").Should().ContainSingle().Which.Should().Be("true");
        var result = await response.Content.ReadFromJsonAsync<HostingResponse>(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        result!.Message.Should().Be("42:query:body-value");
        arguments.Should().ContainSingle();
        arguments.Single().Should().Contain(42).And.Contain("query");
    }

    /// <summary>Verifies a filter still runs when binding fails, and the handler does not.</summary>
    [TestMethod]
    public async Task RunsFiltersButNotTheHandlerWhenBindingFails()
    {
        var invocations = 0;
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync(group => group.AddEndpointFilter(async (context, next) =>
        {
            Interlocked.Increment(ref invocations);
            return await next(context).ConfigureAwait(false);
        })).ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/hosting/requests/not-a-number?Filter=query",
            new HostingRequest { Value = "body-value" },
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invocations.Should().Be(1);
        fixture.State.RequestExecutions.Should().Be(0);
    }

    /// <summary>Verifies a filter can short-circuit the handler with its own result.</summary>
    [TestMethod]
    public async Task LetsFiltersShortCircuitTheHandler()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync(static group => group.AddEndpointFilter(static (_, _) =>
            ValueTask.FromResult<object?>(Results.Text("short-circuited", statusCode: StatusCodes.Status202Accepted)))).ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/hosting/requests/42?Filter=query",
            new HostingRequest { Value = "body-value" },
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await response.Content.ReadAsStringAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false)).Should().Be("short-circuited");
        fixture.State.RequestExecutions.Should().Be(0);
    }
}
