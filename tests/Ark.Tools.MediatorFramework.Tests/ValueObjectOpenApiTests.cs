// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.SystemTextJson;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using System.Text.Json.Nodes;

using Vogen;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>A Vogen value object over a <see cref="Guid"/>.</summary>
[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct OpenApiBookId;

/// <summary>A Vogen value object over an <see cref="int"/>.</summary>
[ValueObject<int>(comparison: ComparisonGeneration.Omit)]
public readonly partial struct OpenApiPageCount;

/// <summary>A body carrying value objects in every shape.</summary>
public sealed record OpenApiBook(
    OpenApiBookId Id,
    OpenApiBookId? ParentId,
    OpenApiPageCount Pages,
    IReadOnlyList<OpenApiBookId> Related,
    OpenApiPageCount[] Chapters);

/// <summary>Vogen's own OpenAPI mapping, which developers may register next to <c>AddArkValueObjectSchemas()</c>.</summary>
[OpenApiMarker<OpenApiBookId>]
[OpenApiMarker<OpenApiPageCount>]
public partial class VogenOpenApiTestMarkers;

[TestClass]
public sealed class ValueObjectOpenApiTests
{
    [TestMethod]
    public async Task ValueObjectsAreDescribedAsTheirPrimitive()
    {
        var document = (await _documentsAsync())["ark"];

        var schemas = document["components"]!["schemas"]!;
        schemas["OpenApiBookId"]!["type"]!.GetValue<string>().Should().Be("string");
        schemas["OpenApiBookId"]!["format"]!.GetValue<string>().Should().Be("uuid");
        schemas["OpenApiPageCount"]!["format"]!.GetValue<string>().Should().Be("int32");
        var properties = schemas["OpenApiBook"]!["properties"]!;
        properties["related"]!["items"]!.ToJsonString().Should().Be("""{"type":"string","format":"uuid"}""");
        properties["chapters"]!["items"]!["format"]!.GetValue<string>().Should().Be("int32");
        var parameters = document["paths"]!["/books/{id}"]!["get"]!["parameters"]!.AsArray();
        parameters[0]!["schema"]!.ToJsonString().Should().Be("""{"type":"string","format":"uuid"}""");
        parameters[1]!["schema"]!["format"]!.GetValue<string>().Should().Be("int32");
    }

    [TestMethod]
    public async Task VogenMappingDoesNotChangeTheDocumentInEitherOrder()
    {
        var documents = await _documentsAsync();

        var expected = _content(documents["ark"]);
        _content(documents["vogen-then-ark"]).Should().Be(expected);
        _content(documents["ark-then-vogen"]).Should().Be(expected);
    }

    [TestMethod]
    public async Task ValueObjectsKeepTheConstraintsOfTheirPrimitive()
    {
        var schemas = (await _documentsAsync())["constrained"]["components"]!["schemas"]!;

        schemas["OpenApiPageCount"]!["minimum"]!.GetValue<int>().Should().Be(1);
        schemas["OpenApiBook"]!["properties"]!["chapters"]!["items"]!["minimum"]!.GetValue<int>().Should().Be(1);
        schemas["OpenApiPageCount"]!["multipleOf"]!.GetValue<int>().Should().Be(2);
        schemas["OpenApiPageCount"]!["examples"]![0]!.GetValue<int>().Should().Be(4);
    }

    [TestMethod]
    public async Task ConcurrentDocumentRequestsEachReplayTheirOwnSchemas()
    {
        // Each request generates its own document; with Vogen's mapping registered after Ark's, every document
        // depends on replaying the schemas rewritten during its own generation.
        var documents = await _documentsAsync(concurrentRequests: 16);

        var expected = _content(documents["ark"][0]);
        documents["ark-then-vogen"].Select(_content).Should().AllBe(expected);
    }

    // Called from a helper, not from the AddOpenApi lambda: the XML comment generator cannot see Vogen's generated
    // extension method, so a lambda that calls it directly is not intercepted and loses the XML descriptions.
    private static OpenApiOptions _vogen(OpenApiOptions options)
    {
        return options.MapVogenTypesInVogenOpenApiTestMarkers();
    }

    private static string _content(JsonNode document)
        => document["paths"]!.ToJsonString() + document["components"]!.ToJsonString();

    private static async Task<Dictionary<string, JsonNode>> _documentsAsync()
    {
        return (await _documentsAsync(concurrentRequests: 1).ConfigureAwait(false)).ToDictionary(static pair => pair.Key, static pair => pair.Value[0], StringComparer.Ordinal);
    }

    private static async Task<Dictionary<string, JsonNode[]>> _documentsAsync(int concurrentRequests)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureHttpJsonOptions(static options =>
            options.SerializerOptions.Converters.Add(new ValueObjectJsonConverterFactory()));
        builder.Services.AddOpenApi("ark", static options => options.AddArkValueObjectSchemas());
        builder.Services.AddOpenApi("vogen-then-ark", static options => _vogen(options).AddArkValueObjectSchemas());
        builder.Services.AddOpenApi("ark-then-vogen", static options => _vogen(options.AddArkValueObjectSchemas()));
        builder.Services.AddOpenApi("constrained", static options => options
            .AddSchemaTransformer(static (schema, context, _) =>
            {
                if (context.JsonTypeInfo.Type == typeof(int))
                {
                    schema.Minimum = "1";
                    schema.MultipleOf = 2;
                    schema.Examples = [JsonValue.Create(4)];
                }
                return Task.CompletedTask;
            })
            .AddArkValueObjectSchemas());
        var app = builder.Build();
        await using var __app = app.ConfigureAwait(false);
        app.MapGet("/books/{id}", static (OpenApiBookId id, OpenApiPageCount pages) => TypedResults.Ok(id.Value));
        app.MapPost("/books", static (OpenApiBook book) => TypedResults.Ok(book));
        app.MapOpenApi();
        await app.StartAsync(app.Lifetime.ApplicationStarted).ConfigureAwait(false);

        using var client = app.GetTestServer().CreateClient();
        var documents = new Dictionary<string, JsonNode[]>(StringComparer.Ordinal);
        foreach (var name in new[] { "ark", "vogen-then-ark", "ark-then-vogen", "constrained" })
        {
            var uri = new Uri("http://localhost/openapi/" + name + ".json");
            var json = await Task.WhenAll(Enumerable.Range(0, concurrentRequests)
                .Select(_ => client.GetStringAsync(uri, app.Lifetime.ApplicationStopping))).ConfigureAwait(false);
            documents[name] = json.Select(static document => JsonNode.Parse(document)!).ToArray();
        }

        return documents;
    }
}
