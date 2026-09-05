// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;

using System.Net;
using System.Text.Json.Nodes;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated OpenAPI documents and schema conventions.</summary>
[TestClass]
public sealed class MinimalApiOpenApiTests
{
    /// <summary>Verifies v1 paths, documentation, schemas, and server-set filtering.</summary>
    [TestMethod]
    public async Task V1DocumentContainsFilteredSchemasAndDocumentation()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var response = await client.GetAsync(
            new Uri("http://localhost/openapi/v1.json"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var document = JsonNode.Parse(body)
            ?? throw new InvalidOperationException("The OpenAPI document was not valid JSON.");
        document["openapi"]?.GetValue<string>().Should().Be("3.1.1");

        var paths = document["paths"]?.AsObject()
            ?? throw new InvalidOperationException("The OpenAPI document had no paths.");
        paths["/api/v1/hosting/requests/{id}"]?["post"].Should().NotBeNull();
        paths["/api/v1/hosting/versioned/{id}"].Should().BeNull();
        paths["/api/v2/hosting/versioned/{id}"].Should().BeNull();

        var requestOperation = paths["/api/v1/hosting/requests/{id}"]!["post"]!;
        requestOperation["summary"]?.GetValue<string>()
            .Should().Be("Deterministic request contract with route, query, body, and server-owned properties.");
        var requestParameters = requestOperation["parameters"]?.AsArray()
            ?? throw new InvalidOperationException("The request operation had no parameters.");
        requestParameters.Single(parameter => parameter?["name"]?.GetValue<string>() == "id")!["description"]?
            .GetValue<string>().Should().Be("Gets or sets the route identifier.");
        requestParameters.Single(parameter => parameter?["name"]?.GetValue<string>() == "Filter")!["description"]?
            .GetValue<string>().Should().Be("Gets or sets the optional query filter.");
        var requestBody = requestOperation["requestBody"]
            ?? throw new InvalidOperationException("The request operation had no request body.");
        var requestSchema = _resolveSchema(document, requestBody["content"]!["application/json"]!["schema"]!);
        requestSchema["properties"]?["value"]?["description"]?.GetValue<string>()
            .Should().Be("Gets or sets the request body value.");

        paths["/api/v1/hosting/openapi"]?["get"]?["summary"]?.GetValue<string>()
            .Should().Be("Query used to expose the generated OpenAPI response schema.");

        var responseSchema = document["components"]?["schemas"]?["HostingOpenApiResponse"]
            ?? throw new InvalidOperationException("The OpenAPI response schema was not generated.");
        responseSchema["properties"]?["serverStamp"].Should().BeNull();
        responseSchema["properties"]?["date"]?["format"]?.GetValue<string>().Should().Be("date");
        responseSchema["properties"]?["shape"]?["oneOf"]?.AsArray().Count.Should().Be(1);
    }

    /// <summary>Verifies v2 contains active v2 operations without other document versions.</summary>
    [TestMethod]
    public async Task V2DocumentContainsOnlyV2Operations()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var response = await client.GetAsync(
            new Uri("http://localhost/openapi/v2.json"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var document = JsonNode.Parse(body)
            ?? throw new InvalidOperationException("The OpenAPI document was not valid JSON.");
        var paths = document["paths"]?.AsObject()
            ?? throw new InvalidOperationException("The OpenAPI document had no paths.");

        paths["/api/v2/hosting/requests/{id}"]?["post"].Should().NotBeNull();
        paths["/api/v2/hosting/versioned/{id}"]?["get"].Should().NotBeNull();
        paths["/api/v1/hosting/requests/{id}"].Should().BeNull();
        paths["/api/v3/hosting/versioned/{id}"].Should().BeNull();
    }

    /// <summary>Verifies every v1 operation documents its exact status codes and problem responses.</summary>
    [TestMethod]
    public async Task V1OperationsDocumentStatusCodesAndProblemResponses()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        var document = await _getDocumentAsync(app, "v1").ConfigureAwait(false);
        var paths = document["paths"]!.AsObject();

        foreach (var (path, operations) in paths)
        {
            foreach (var (verb, operation) in operations!.AsObject())
            {
                var responses = operation!["responses"]?.AsObject();
                responses.Should().NotBeNull($"operation {verb} {path} must document responses");
                foreach (var problemStatus in new[] { "400", "500" })
                {
                    responses![problemStatus].Should().NotBeNull($"operation {verb} {path} must document {problemStatus}");
                    responses[problemStatus]!["content"]?["application/problem+json"]
                        .Should().NotBeNull($"operation {verb} {path} must document {problemStatus} as problem+json");
                }

                var isAuthorized = string.Equals(path, "/api/v1/hosting/authorized", StringComparison.Ordinal);
                if (isAuthorized)
                {
                    responses!["403"]?["content"]?["application/problem+json"]
                        .Should().NotBeNull("the authorized operation must document 403 as problem+json");
                }
                else
                {
                    responses!["403"].Should().BeNull($"anonymous operation {verb} {path} must not document 403");
                }
            }
        }

        var command = paths["/api/v1/hosting/commands"]!["post"]!["responses"]!.AsObject();
        command["204"].Should().NotBeNull();
        command["202"].Should().BeNull();

        var busCommand = paths["/api/v1/hosting/bus-commands"]!["post"]!["responses"]!.AsObject();
        busCommand["204"].Should().NotBeNull();
        busCommand["202"].Should().BeNull();

        var status = paths["/api/v1/hosting/status"]!["post"]!["responses"]!.AsObject();
        status["201"].Should().NotBeNull();

        var notFound = paths["/api/v1/hosting/not-found"]!["get"]!["responses"]!.AsObject();
        notFound["404"].Should().NotBeNull();
        notFound["200"].Should().NotBeNull();

        var noContent = paths["/api/v1/hosting/no-content"]!["post"]!["responses"]!.AsObject();
        noContent["204"].Should().NotBeNull();
    }

    /// <summary>Verifies ETag-marked properties stay in the documented request and response schemas.</summary>
    [TestMethod]
    public async Task ETagPropertiesRemainInDocumentedSchemas()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        var document = await _getDocumentAsync(app, "v1").ConfigureAwait(false);

        var requestBody = document["paths"]!["/api/v1/hosting/etag/{id}"]!["put"]!["requestBody"]!;
        var requestSchema = _resolveSchema(document, requestBody["content"]!["application/json"]!["schema"]!);
        requestSchema["properties"]?["eTag"].Should().NotBeNull("the [ETag] request property must not be filtered from the schema");

        var response = document["paths"]!["/api/v1/hosting/etag/{id}"]!["get"]!["responses"]!["200"]!;
        var responseSchema = _resolveSchema(document, response["content"]!["application/json"]!["schema"]!);
        responseSchema["properties"]?["token"].Should().NotBeNull("the [ETag] response property must stay in the response schema");
    }

    private static async Task<JsonNode> _getDocumentAsync(
        Microsoft.AspNetCore.Builder.WebApplication app,
        string documentName)
    {
        using var client = app.GetTestServer().CreateClient();
        using var response = await client.GetAsync(
            new Uri($"http://localhost/openapi/{documentName}.json"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonNode.Parse(body)
            ?? throw new InvalidOperationException("The OpenAPI document was not valid JSON.");
    }

    private static JsonNode _resolveSchema(JsonNode document, JsonNode schema)
    {
        var reference = schema["$ref"]?.GetValue<string>();
        if (reference is null)
            return schema;

        var name = reference[(reference.LastIndexOf('/') + 1)..];
        return document["components"]?["schemas"]?[name]
            ?? throw new InvalidOperationException($"The schema reference '{reference}' was not found.");
    }
}
