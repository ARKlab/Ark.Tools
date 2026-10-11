// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Web.WebInterface;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using SimpleInjector.Lifestyles;

using System.Reflection;
using System.Net;
using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Core.Web.Tests;

/// <summary>Verifies generated Book transport boundaries.</summary>
[TestClass]
public sealed class BookTransportBoundaryTests
{
    /// <summary>Dispatches Book edition and streaming calls through generated gRPC code.</summary>
    [TestMethod]
    public async Task GeneratedGrpcBooksServiceDispatchesEditionAndStream()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);
        var container = host.Container;
        // The generated service reads the principal from the forwarded accessor, as a gRPC call would set it.
        host.App.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("scope", ApplicationScopes.BookRead)],
                "boundary-test")),
        };

        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var generatedEndpointsType = typeof(SampleHost).Assembly.GetType(
            "Ark.Tools.MediatorFramework.Generated.ArkGeneratedEndpoints")
            ?? throw new InvalidOperationException("Generated gRPC endpoint type was not found.");
        var serviceType = generatedEndpointsType.GetNestedType(
            "BooksV1GrpcService",
            BindingFlags.Public)
            ?? throw new InvalidOperationException("Generated Books gRPC service type was not found.");
        var service = Activator.CreateInstance(serviceType, container)
            ?? throw new InvalidOperationException("Generated Books gRPC service could not be created.");
        var describeMethod = serviceType.GetMethod("DescribeBookEditionRequest_V1Async")
            ?? throw new InvalidOperationException("Generated Book edition gRPC method was not found.");
        var streamMethod = serviceType.GetMethod("StreamBooksQuery_V1Async")
            ?? throw new InvalidOperationException("Generated Book stream gRPC method was not found.");
        var editionTask = (ValueTask<BookEditionDescription>)describeMethod.Invoke(service,
        [
            new DescribeBookEditionRequest.V1
            {
                Edition = new PrintBookEdition
                {
                    Format = EditionFormat.From("Paperback"),
                    PageCount = PageCount.From(320),
                },
            },
            default(ProtoBuf.Grpc.CallContext),
        ])!;
        var edition = await editionTask.ConfigureAwait(false);
        var items = new List<BookStreamItem>();
        var stream = (IAsyncEnumerable<BookStreamItem>)streamMethod.Invoke(service,
        [
            new StreamBooksQuery.V1 { Count = 2 },
            default(ProtoBuf.Grpc.CallContext),
        ])!;
        await foreach (var item in stream.WithCancellation(CancellationToken.None).ConfigureAwait(false))
        {
            items.Add(item);
        }

        edition.Description.Should().Be("Paperback print edition with 320 pages");
        items.Select(static item => item.Index).Should().Equal(0, 1);
    }

    /// <summary>Maps the generated Book stream and edition routes into the HTTP endpoint set.</summary>
    [TestMethod]
    public async Task GeneratedHttpBooksEndpointsExposeExpectedRoutes()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);
        var app = host.App;

        var routes = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        routes.Should().Contain("/api/v1/books/stream");
        routes.Should().Contain("/api/v1/books/editions/describe");
        routes.Should().Contain("/api/v1/books/bulk");
    }

    /// <summary>Returns 401 for missing, non-bearer, and malformed bearer credentials.</summary>
    [TestMethod]
    public async Task InvalidBearerCredentialsReturnUnauthorized()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);
        var app = host.App;

        foreach (var authorization in new string?[] { null, "Basic abc", "Bearer " + "not-a-jwt" })
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri("/api/v1/books/" + Guid.NewGuid(), UriKind.Relative));
            if (authorization is not null)
                request.Headers.TryAddWithoutValidation("Authorization", authorization);

            using var response = await app.GetTestClient().SendAsync(
                request,
                app.Lifetime.ApplicationStopping).ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.WwwAuthenticate.Should().NotBeEmpty();
        }
    }
}
