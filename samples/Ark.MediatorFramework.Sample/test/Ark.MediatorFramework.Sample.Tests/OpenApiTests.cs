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
        using var response = await context.Client.GetAsync("/openapi/v1.json").ConfigureAwait(false);
        response.IsSuccessStatusCode.Should().BeTrue();

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false))
            .Should().NotBeNull().Subject;
        document["openapi"]?.GetValue<string>().Should().Be("3.1.0");

        var schemas = document["components"]?["schemas"];
        schemas?["GreetingResponse"]?["properties"]?["eTag"]?["type"]?.GetValue<string>().Should().Be("string");
        schemas?["CreateGreetingRequest"]?["properties"]?["date"]?["format"]?.GetValue<string>().Should().Be("date");
        schemas?["ShapeDescription"]?["properties"]?["shape"]?["oneOf"]?.AsArray().Count.Should().Be(2);

        var paths = document["paths"];
        paths?["/api/v1/greetings/{id}"]?["get"].Should().NotBeNull();
        paths?["/api/v1/greeting-cards/{id}"]?["post"]?["requestBody"]?["content"]?["multipart/form-data"]
            .Should().NotBeNull();
    }

    /// <summary>The v2 document contains the introduced versioned operation.</summary>
    [TestMethod]
    public async Task V2DocumentContainsIntroducedOperation()
    {
        using var context = new SampleTestContext();
        using var response = await context.Client.GetAsync("/openapi/v2.json").ConfigureAwait(false);
        response.IsSuccessStatusCode.Should().BeTrue();

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false))
            .Should().NotBeNull().Subject;
        document["paths"]?["/api/v2/greetings-v2/{id}"]?["get"].Should().NotBeNull();
    }

    /// <summary>The OpenAPI middleware exposes the same documents as YAML.</summary>
    [TestMethod]
    public async Task YamlDocumentIsReachable()
    {
        using var context = new SampleTestContext();
        using var response = await context.Client.GetAsync("/openapi/v1.yaml").ConfigureAwait(false);
        response.IsSuccessStatusCode.Should().BeTrue();

        var yaml = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        yaml.Should().Contain("openapi: 3.1.0");
    }
}
