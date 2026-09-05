// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Auth;
using Ark.MediatorFramework.Sample.WebInterface;

using Ark.Tools.Outbox;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Rebus.Transport.InMem;

using System.Net;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies the generated Server-Sent Events routes of the sample host.</summary>
[TestClass]
public sealed class SseEndpointTests
{
    /// <summary>Maps a sibling SSE route next to each contract that declares <c>[Sse]</c>.</summary>
    [TestMethod]
    public async Task GeneratedSseRoutesAreMappedNextToTheirHttpEndpoint()
    {
        await using var host = await _startAsync().ConfigureAwait(false);

        var routes = host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        routes.Should().Contain("/api/v1/books/{bookId}/reviews/poller");
        routes.Should().Contain("/api/v1/books/stream/stream");
    }

    /// <summary>Requires the same authorization as the underlying query.</summary>
    [TestMethod]
    public async Task SseRouteRejectsUnauthenticatedCallers()
    {
        await using var host = await _startAsync().ConfigureAwait(false);

        using var response = await host.GetTestClient().GetAsync(
            new Uri("/api/v1/books/stream/stream", UriKind.Relative),
            host.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Frames the items of a streaming query as events.</summary>
    [TestMethod]
    public async Task SseRouteFramesStreamingQueryItemsAsEvents()
    {
        await using var host = await _startAsync().ConfigureAwait(false);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri("/api/v1/books/stream/stream?Count=3&DelayMilliseconds=0", UriKind.Relative));
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            "Bearer " + new JwtTokenBuilder().AddSubject("sse-user").AddScope(ApplicationScopes.BookRead).Build());

        using var response = await host.GetTestClient().SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            host.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        var body = await response.Content.ReadAsStringAsync(host.Lifetime.ApplicationStopping).ConfigureAwait(false);
        body.Split("event: StreamBooksQuery_V1").Should().HaveCount(4);
        body.Should().Contain("\"index\":2");
    }

    private static async Task<WebApplication> _startAsync()
    {
        var network = new InMemNetwork();
        var dataContextFactory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var container = SampleComposition.BuildContainer(
            network,
            useSqlStore: false,
            dataContextFactory: dataContextFactory);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(SampleHost).Assembly.GetName().Name,
            EnvironmentName = "IntegrationTests",
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.UseTestServer();

        var startup = SampleHost.Configure(
            builder,
            container,
            network,
            useSqlStore: false,
            sharedDataContextFactory: dataContextFactory);
        var app = builder.Build();
        startup.Configure(app);
        await app.StartAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        return app;
    }
}
