// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Ark.MediatorFramework.Sample.AzureFunctions.Tests;

/// <summary>
/// Demonstrates end-to-end testing of a Mediator-Framework application hosted as an
/// Azure Function: the built host is launched with Azure Functions Core Tools and
/// exercised over real HTTP, exactly as a deployed Function App would be.
/// </summary>
[TestClass]
public sealed class GreetingFunctionTests
{
    private static FunctionHostFixture? _host;
    private static HttpClient? _client;

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        _host = await FunctionHostFixture.StartAsync(context.CancellationToken).ConfigureAwait(false);
        _client = new HttpClient { BaseAddress = _host.BaseAddress };
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        _client?.Dispose();
        if (_host is not null)
            await _host.DisposeAsync().ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public async Task HealthCheckIsExposedAnonymously()
    {
        using var response = await _client!.GetAsync(
            new Uri("healthCheck", UriKind.Relative),
            TestContext.CancellationToken).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public async Task GreetingsRequireAuthentication()
    {
        using var response = await _client!.GetAsync(
            new Uri("api/v1/greetings", UriKind.Relative),
            TestContext.CancellationToken).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    [TestCategory("AzureFunctionsBoundary")]
    public async Task AuthenticatedUserCanSearchGreetings()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("api/v1/greetings", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokenBuilder.Build("sample-user"));

        using var response = await _client!.SendAsync(request, TestContext.CancellationToken).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<GreetingPageDto>(TestContext.CancellationToken).ConfigureAwait(false);
        page.Should().NotBeNull();
        page!.Data.Should().NotBeNull();
    }

    private sealed record GreetingPageDto(IReadOnlyList<object> Data, int Count);
}
