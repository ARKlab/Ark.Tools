// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

using System.Text.Json.Nodes;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies the generated OpenAPI 3.1 documents exposed by the sample.</summary>
[TestClass]
public sealed class OpenApiTests
{
    /// <summary>The v1 document contains representative generated contract schemas and operations.</summary>
    [TestMethod]
    public async Task V1DocumentContainsRepresentativeSchemasAndOperations()
    {
        using var context = new SampleTestContext();
        using var response = await context.Client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative)).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, responseBody);

        var document = JsonNode.Parse(responseBody)
            ?? throw new InvalidOperationException("The OpenAPI document was not valid JSON.");
        document["openapi"]?.GetValue<string>().Should().Be("3.1.1");

        var schemas = document["components"]?["schemas"];
        schemas?["GreetingResponse"]?.ToJsonString().Should().Contain("\"eTag\"");
        schemas?["LocalDate"]?.ToJsonString().Should().Contain("\"format\":\"date\"");
        schemas?["ShapeDescription"]?["properties"]?["shape"]?["oneOf"]?.AsArray().Count.Should().Be(2);

        var paths = document["paths"];
        paths?["/api/v1/greetings/{id}"]?["get"].Should().NotBeNull();
        paths?["/api/v1/greeting-cards/{id}"]?["post"]?["requestBody"]?["content"]?["multipart/form-data"]
            .Should().NotBeNull();
        paths?["/api/v1/greetings/{id}"]?["put"]?["summary"]?.GetValue<string>()
            .Should().Be("Updates a greeting using an opaque ETag precondition.");
        paths?["/api/v1/greetings/{id}"]?["put"]?["parameters"]?.AsArray()
            .FirstOrDefault(parameter => parameter?["name"]?.GetValue<string>() == "id")?["description"]
            ?.GetValue<string>().Should().Be("Gets the greeting identifier.");
        document["components"]?["schemas"]?["UpdateGreetingMessageRequest"]?["properties"]?["message"]?["description"]
            ?.GetValue<string>()
            .Should().Be("Gets the replacement message.");
    }

    /// <summary>The v2 document contains the introduced versioned operation.</summary>
    [TestMethod]
    public async Task V2DocumentContainsIntroducedOperation()
    {
        using var context = new SampleTestContext();
        using var response = await context.Client.GetAsync(new Uri("/openapi/v2.json", UriKind.Relative)).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, responseBody);

        var document = JsonNode.Parse(responseBody)
            ?? throw new InvalidOperationException("The OpenAPI document was not valid JSON.");
        document["paths"]?["/api/v2/greetings-v2/{id}"]?["get"].Should().NotBeNull();
    }

    /// <summary>Every generated operation advertises the standard problem responses.</summary>
    [TestMethod]
    public async Task EveryOperationAdvertisesStandardProblemResponses()
    {
        foreach (var version in new[] { "v1", "v2" })
        {
            using var context = new SampleTestContext();
            using var response = await context.Client.GetAsync(new Uri($"/openapi/{version}.json", UriKind.Relative))
                .ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, responseBody);

            var document = JsonNode.Parse(responseBody)
                ?? throw new InvalidOperationException("The OpenAPI document was not valid JSON.");
            var paths = document["paths"]?.AsObject()
                ?? throw new InvalidOperationException("The OpenAPI document had no paths.");
            foreach (var path in paths)
            {
                foreach (var operation in path.Value?.AsObject()
                    .Where(static item => item.Key is "get" or "post" or "put" or "patch" or "delete")
                    .Select(static item => item.Value)
                    .OfType<JsonObject>() ?? [])
                {
                    var responses = operation["responses"]?.AsObject()
                        ?? throw new InvalidOperationException("An operation had no responses.");
                    responses["400"]?["content"]?["application/problem+json"].Should().NotBeNull();
                    responses["500"]?["content"]?["application/problem+json"].Should().NotBeNull();
                    // An empty security array means "anonymous" (explicit override of document-level security).
                    // Null means "inherits document-level security" → secured.
                    var operationSecurity = operation["security"]?.AsArray();
                    var isAnonymous = operationSecurity is not null && operationSecurity.Count == 0;
                    if (!isAnonymous)
                        responses["403"]?["content"]?["application/problem+json"].Should().NotBeNull();
                }
            }
        }
    }

    /// <summary>The OpenAPI middleware exposes the same documents as YAML.</summary>
    [TestMethod]
    public async Task YamlDocumentIsReachable()
    {
        using var context = new SampleTestContext();
        using var response = await context.Client.GetAsync(new Uri("/openapi/v1.yaml", UriKind.Relative)).ConfigureAwait(false);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var yaml = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        yaml.Should().Contain("openapi: '3.1.1'");
    }
}
