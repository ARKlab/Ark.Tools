// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Reference.Core.Tests.Init;
using Ark.Tools.Compliance;
using Ark.Tools.Compliance.OpenApi;

using AwesomeAssertions;

using Reqnroll;

using System.Text.Json;

namespace Ark.Reference.Core.Tests.Features;

[Binding]
public sealed class SwaggerSteps
{
    private readonly TestClient _client;

    public SwaggerSteps(TestClient client)
    {
        _client = client;
    }

    [Then("the Swagger Book author schema is a classified primitive")]
    public void ThenTheSwaggerBookAuthorSchemaIsAClassifiedPrimitive()
    {
        using var document = JsonDocument.Parse(_client.ReadAsString());
        var schemas = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas");
        var authorSchemas = schemas.EnumerateObject()
            .SelectMany(static schema => _properties(schema.Value)
                .Where(static property => property.Name.Equals("author", StringComparison.OrdinalIgnoreCase))
                .Select(property => property.Value))
            .ToArray();

        authorSchemas.Should().NotBeEmpty();
        foreach (var authorSchema in authorSchemas)
        {
            var schema = _resolveSchema(schemas, authorSchema);
            schema.GetProperty("type").GetString().Should().Be("string");
            schema.GetProperty(SensitiveValueSchemaDescriptor.ClassificationExtension)
                .GetString()
                .Should()
                .Be("Ark:PersonalData");
            schema.TryGetProperty("value", out _).Should().BeFalse();
        }
    }

    private static IEnumerable<JsonProperty> _properties(JsonElement schema)
    {
        return schema.TryGetProperty("properties", out var properties)
            ? properties.EnumerateObject()
            : [];
    }

    private static JsonElement _resolveSchema(JsonElement schemas, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference))
            return schema;

        var name = reference.GetString()?.Split('/').Last()
            ?? throw new InvalidOperationException("OpenAPI schema reference is empty.");
        return schemas.GetProperty(name);
    }
}
