// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Net.Http.Json;
using System.Text.Json;

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.WebInterface;

using AwesomeAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

using Rebus.Transport.InMem;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>
/// Self-tests for the OpenAPI generation, System.Text.Json polymorphism and route-based API
/// versioning wired on top of the source-generated Minimal API endpoints.
/// </summary>
[TestClass]
public sealed class OpenApiPolymorphismVersioningTests
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions().ConfigureArkDefaults();
    private static InMemNetwork _network = null!;
    private static IHost _host = null!;
    private static HttpClient _client = null!;
    private static Container _container = null!;

    /// <summary>Builds the shared container and HTTP test server once.</summary>
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        _network = new InMemNetwork();
        _container = SampleComposition.BuildContainer(_network);

        var startup = new SampleStartup(_container);
        _host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(startup.ConfigureServices)
                .Configure(startup.Configure))
            .Build();

        _host.Start();
        _client = _host.GetTestServer().CreateClient();
    }

    /// <summary>Disposes the shared server.</summary>
    [ClassCleanup]
    public static void ClassCleanup()
    {
        _client?.Dispose();
        _host?.Dispose();
    }

    [TestMethod]
    public async Task OpenApi_v1_document_contains_the_v1_endpoints_and_not_the_v2_endpoint()
    {
        var paths = await GetDocumentPathsAsync("v1").ConfigureAwait(false);

        paths.Should().Contain("/api/v1/greetings");
        paths.Should().Contain("/api/v1/greetings/{id}");
        paths.Should().Contain("/api/v1/shapes/describe");
        paths.Should().NotContain("/api/v2/greetings/{id}", "v2 endpoints belong to the v2 document");
    }

    [TestMethod]
    public async Task OpenApi_v2_document_contains_the_v2_endpoint_and_not_the_v1_endpoints()
    {
        var paths = await GetDocumentPathsAsync("v2").ConfigureAwait(false);

        paths.Should().Contain("/api/v2/greetings-v2/{id}");
        paths.Should().NotContain("/api/v1/greetings", "v1 endpoints belong to the v1 document");
    }

    [TestMethod]
    public async Task OpenApi_v1_document_describes_greeting_request_response_and_NodaTime_strings()
    {
        using var document = await GetDocumentAsync("v1").ConfigureAwait(false);
        var root = document.RootElement;
        var operation = root.GetProperty("paths").GetProperty("/api/v1/greetings").GetProperty("post");
        var requestSchema = operation.GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        var responseSchema = operation.GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

        requestSchema.GetProperty("$ref").GetString().Should().Be("#/components/schemas/CreateGreetingRequest");
        responseSchema.GetProperty("$ref").GetString().Should().Be("#/components/schemas/GreetingResponse");

        var schemas = root.GetProperty("components").GetProperty("schemas");
        var expectedFormats = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LocalDate"] = "date",
            ["LocalDateTime"] = "date-time",
            ["OffsetDateTime"] = "date-time",
            ["Period"] = "duration",
        };

        foreach (var (schemaName, format) in expectedFormats)
        {
            var schema = schemas.GetProperty(schemaName);
            schema.GetProperty("format").GetString().Should().Be(format);
        }
    }

    [TestMethod]
    public async Task OpenApi_v1_document_exposes_polymorphic_discriminator_and_nested_references()
    {
        using var document = await GetDocumentAsync("v1").ConfigureAwait(false);

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var shape = schemas.GetProperty("Shape");
        var discriminator = shape.GetProperty("discriminator");

        shape.GetProperty("oneOf").GetArrayLength().Should().Be(2);
        discriminator.GetProperty("propertyName").GetString().Should().Be("kind");
        discriminator.GetProperty("mapping").GetProperty("Circle").GetString()
            .Should().Be("#/components/schemas/Circle");
        discriminator.GetProperty("mapping").GetProperty("Square").GetString()
            .Should().Be("#/components/schemas/Square");
        schemas.GetProperty("Circle").GetProperty("properties").TryGetProperty("radius", out _).Should().BeTrue();
        schemas.GetProperty("Square").GetProperty("properties").TryGetProperty("side", out _).Should().BeTrue();

        schemas.GetProperty("ShapeDescription")
            .GetProperty("properties")
            .GetProperty("metadata")
            .GetProperty("$ref")
            .GetString()
            .Should().Be("#/components/schemas/ShapeEnvelope");
        schemas.GetProperty("ShapeEnvelope")
            .GetProperty("properties")
            .GetProperty("featuredShape")
            .GetProperty("$ref")
            .GetString()
            .Should().Be("#/components/schemas/Shape");
    }

    [TestMethod]
    public async Task Polymorphic_circle_round_trips_through_the_generated_endpoint()
    {
        var payload = new { shape = new { kind = "Circle", radius = 2.0 } };

        var post = await _client.PostAsJsonAsync("/api/v1/shapes/describe", payload).ConfigureAwait(false);
        post.EnsureSuccessStatusCode();

        var description = await post.Content.ReadFromJsonAsync<ShapeDescription>().ConfigureAwait(false);

        description.Should().NotBeNull();
        description!.Shape.Should().BeOfType<Circle>("the discriminator must select the concrete subtype");
        ((Circle)description.Shape).Radius.Should().Be(2.0);
        description.Area.Should().BeApproximately(Math.PI * 2.0 * 2.0, 1e-9);
        description.Metadata.FeaturedShape.Should().BeOfType<Circle>();
    }

    [TestMethod]
    public async Task Polymorphic_square_round_trips_through_the_generated_endpoint()
    {
        var payload = new { shape = new { kind = "Square", side = 3.0 } };

        var post = await _client.PostAsJsonAsync("/api/v1/shapes/describe", payload).ConfigureAwait(false);
        post.EnsureSuccessStatusCode();

        var description = await post.Content.ReadFromJsonAsync<ShapeDescription>().ConfigureAwait(false);

        description.Should().NotBeNull();
        description!.Shape.Should().BeOfType<Square>();
        ((Square)description.Shape).Side.Should().Be(3.0);
        description.Area.Should().Be(9.0);
        description.Metadata.FeaturedShape.Should().BeOfType<Square>();
    }

    [TestMethod]
    public async Task Versioned_v2_endpoint_evolves_the_contract()
    {
        // Seed a greeting through the v1 create endpoint, then read it back through the v2 contract.
        var post = await _client.PostAsJsonAsync("/api/v1/greetings", new { name = "Versioning" }).ConfigureAwait(false);
        post.EnsureSuccessStatusCode();
        var greeting = await post.Content.ReadFromJsonAsync<GreetingResponse>(JsonOptions).ConfigureAwait(false);
        greeting.Should().NotBeNull();

        var v2 = await _client.GetFromJsonAsync<GreetingResponseV2>($"/api/v2/greetings-v2/{greeting!.Id}").ConfigureAwait(false);

        v2.Should().NotBeNull();
        v2!.Id.Should().Be(greeting.Id);
        v2.Message.Should().Be(greeting.Message);
        v2.MessageLength.Should().Be(greeting.Message.Length, "v2 adds the message length to the response");
    }

    [TestMethod]
    public async Task Retired_v1_route_is_not_available_in_v2()
    {
        var response = await _client.GetAsync(
            new Uri($"/api/v2/greetings/{Guid.NewGuid()}", UriKind.Relative)).ConfigureAwait(false);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    private static async Task<JsonDocument> GetDocumentAsync(string documentName)
    {
        var json = await _client.GetStringAsync(new Uri($"/openapi/{documentName}.json", UriKind.Relative)).ConfigureAwait(false);
        return JsonDocument.Parse(json);
    }

    private static async Task<IReadOnlyCollection<string>> GetDocumentPathsAsync(string documentName)
    {
        using var document = await GetDocumentAsync(documentName).ConfigureAwait(false);
        return document.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
    }
}
