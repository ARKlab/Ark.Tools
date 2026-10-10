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
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureHttpJsonOptions(static options =>
            options.SerializerOptions.Converters.Add(new ValueObjectJsonConverterFactory()));
        builder.Services.AddOpenApi("ark", static options => options.AddArkValueObjectSchemas());
        builder.Services.AddOpenApi("vogen-then-ark", static options => _vogen(options).AddArkValueObjectSchemas());
        builder.Services.AddOpenApi("ark-then-vogen", static options => _vogen(options.AddArkValueObjectSchemas()));
        var app = builder.Build();
        await using var __app = app.ConfigureAwait(false);
        app.MapGet("/books/{id}", static (OpenApiBookId id, OpenApiPageCount pages) => TypedResults.Ok(id.Value));
        app.MapPost("/books", static (OpenApiBook book) => TypedResults.Ok(book));
        app.MapOpenApi();
        await app.StartAsync(app.Lifetime.ApplicationStarted).ConfigureAwait(false);

        using var client = app.GetTestServer().CreateClient();
        var documents = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var name in new[] { "ark", "vogen-then-ark", "ark-then-vogen" })
        {
            var json = await client.GetStringAsync(new Uri("http://localhost/openapi/" + name + ".json"), app.Lifetime.ApplicationStopping).ConfigureAwait(false);
            documents[name] = JsonNode.Parse(json)!;
        }

        return documents;
    }
}
