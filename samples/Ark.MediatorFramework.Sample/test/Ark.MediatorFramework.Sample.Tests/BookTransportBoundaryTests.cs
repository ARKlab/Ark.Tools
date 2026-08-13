// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.WebInterface;

using Ark.Tools.Outbox;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Rebus.Transport.InMem;

using SimpleInjector.Lifestyles;

using System.Reflection;
using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies generated Book transport boundaries.</summary>
[TestClass]
public sealed class BookTransportBoundaryTests
{
    /// <summary>Dispatches Book edition and streaming calls through generated gRPC code.</summary>
    [TestMethod]
    public async Task GeneratedGrpcBooksServiceDispatchesEditionAndStream()
    {
        var network = new InMemNetwork();
        var dataContextFactory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var container = SampleComposition.BuildContainer(
            network,
            useSqlStore: false,
            dataContextFactory: dataContextFactory);
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("scope", ApplicationScopes.BookRead)],
                    "boundary-test")),
            },
        };
        container.RegisterInstance<IHttpContextAccessor>(httpContextAccessor);

        try
        {
            container.Verify();
            await using var scope = AsyncScopedLifestyle.BeginScope(container);
            var generatedEndpointsType = typeof(SampleHost).Assembly.GetType(
                "Ark.MediatorFramework.Generated.ArkGeneratedEndpoints")
                ?? throw new InvalidOperationException("Generated gRPC endpoint type was not found.");
            var serviceType = generatedEndpointsType.GetNestedType(
                "BooksV1GrpcService",
                BindingFlags.Public)
                ?? throw new InvalidOperationException("Generated Books gRPC service type was not found.");
            var service = Activator.CreateInstance(serviceType, container)
                ?? throw new InvalidOperationException("Generated Books gRPC service could not be created.");
            var describeMethod = serviceType.GetMethod("DescribeBookEditionRequestAsync")
                ?? throw new InvalidOperationException("Generated Book edition gRPC method was not found.");
            var streamMethod = serviceType.GetMethod("StreamBooksQueryAsync")
                ?? throw new InvalidOperationException("Generated Book stream gRPC method was not found.");
            var editionTask = (ValueTask<BookEditionDescription>)describeMethod.Invoke(service,
            [
                new DescribeBookEditionRequest
                {
                    Edition = new PrintBookEdition
                    {
                        Format = "Paperback",
                        PageCount = 320,
                    },
                },
                default(ProtoBuf.Grpc.CallContext),
            ])!;
            var edition = await editionTask.ConfigureAwait(false);
            var items = new List<BookStreamItem>();
            var stream = (IAsyncEnumerable<BookStreamItem>)streamMethod.Invoke(service,
            [
                new StreamBooksQuery { Count = 2 },
                default(ProtoBuf.Grpc.CallContext),
            ])!;
            await foreach (var item in stream.WithCancellation(CancellationToken.None).ConfigureAwait(false))
            {
                items.Add(item);
            }

            edition.Description.Should().Be("Paperback print edition with 320 pages");
            items.Select(item => item.Index).Should().Equal(0, 1);
        }
        finally
        {
            await container.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Maps the generated Book stream and edition routes into the HTTP endpoint set.</summary>
    [TestMethod]
    public async Task GeneratedHttpBooksEndpointsExposeExpectedRoutes()
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
        await using var app = builder.Build();
        startup.Configure(app);
        await app.StartAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        var routes = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        routes.Should().Contain("/api/v1/books/stream");
        routes.Should().Contain("/api/v2/books/stream");
        routes.Should().Contain("/api/v1/books/editions/describe");
        routes.Should().Contain("/api/v2/books/editions/describe");
    }
}
