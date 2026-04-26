// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using AwesomeAssertions;

using Flurl.Http;

using Reqnroll;

using System.Text.Json;

namespace WebApplicationDemo.Tests;

/// <summary>
/// Step definitions for verifying the OpenAPI/Swagger specification content,
/// including NodaTime type mapping, OData endpoint presence, model versioning,
/// and Swashbuckle operation filter behavior.
/// </summary>
[Binding]
public sealed class SwaggerSpecSteps : IDisposable
{
#pragma warning disable CA2213 // _client lifetime is managed by the Reqnroll scenario container
    private readonly IFlurlClient _client;
#pragma warning restore CA2213
    private JsonDocument? _swaggerDoc;

    public SwaggerSpecSteps(ScenarioContext sctx)
    {
        _client = sctx.ScenarioContainer.Resolve<IFlurlClient>();
    }

    [When(@"I fetch the swagger spec for version (.*)")]
    public async Task WhenIFetchSwaggerSpecForVersion(string version)
    {
        _swaggerDoc?.Dispose();
        var json = await _client.Request($"/swagger/docs/{version}").GetStringAsync().ConfigureAwait(false);
        _swaggerDoc = JsonDocument.Parse(json);
    }

    private JsonDocument Doc => _swaggerDoc ?? throw new InvalidOperationException("Fetch the swagger spec first");

    // ── Paths ──────────────────────────────────────────────────────────────────

    [Then(@"the swagger spec contains path matching (.*)")]
    public void ThenSwaggerSpecContainsPathMatching(string pathFragment)
    {
        var paths = Doc.RootElement.GetProperty("paths");
        var found = paths.EnumerateObject().Any(p =>
            p.Name.Contains(pathFragment, StringComparison.OrdinalIgnoreCase));
        found.Should().BeTrue($"Swagger paths should contain '{pathFragment}'");
    }

    [Then(@"the swagger spec does not contain path matching (.*)")]
    public void ThenSwaggerSpecDoesNotContainPathMatching(string pathFragment)
    {
        var paths = Doc.RootElement.GetProperty("paths");
        var found = paths.EnumerateObject().Any(p =>
            p.Name.Contains(pathFragment, StringComparison.OrdinalIgnoreCase));
        found.Should().BeFalse($"Swagger paths should NOT contain '{pathFragment}'");
    }

    // ── NodaTime schema mapping via LocalDateRange ─────────────────────────────
    // NodaTime types are mapped inline via MapType<T>() in SupportNodaTimeExtensions,
    // not as standalone named schemas. We verify the mapping is applied correctly
    // by inspecting LocalDateRange.start/end (which are LocalDate) and
    // Entity.V1.Output.date (which is LocalDate?).

    [Then(@"the swagger spec LocalDateRange schema has start property with format date")]
    public void ThenLocalDateRangeHasStartWithFormatDate()
    {
        var schema = GetSchema("LocalDateRange");
        schema.TryGetProperty("properties", out var props).Should().BeTrue("LocalDateRange should have 'properties'");
        props.TryGetProperty("start", out var startProp).Should().BeTrue("LocalDateRange should have 'start' property");

        startProp.TryGetProperty("format", out var format).Should().BeTrue("LocalDateRange.start should have 'format'");
        format.GetString().Should().Be("date", "LocalDate should be mapped to format 'date' by MapNodaTimeTypes()");
    }

    [Then(@"the swagger spec Entity.V1.Output allOf has date property with format date")]
    public void ThenEntityOutputAllOfHasDateWithFormatDate()
    {
        var schema = GetSchema("Entity.V1.Output");
        schema.TryGetProperty("allOf", out var allOf).Should().BeTrue("Entity.V1.Output should use allOf for inheritance");

        // The second allOf entry contains the output-specific properties
        JsonElement? dateProperty = null;
        foreach (var allOfEntry in allOf.EnumerateArray())
        {
            if (allOfEntry.TryGetProperty("properties", out var props)
                && props.TryGetProperty("date", out var dateProp))
            {
                dateProperty = dateProp;
                break;
            }
        }

        dateProperty.Should().NotBeNull("Entity.V1.Output allOf should have a 'date' property somewhere");
        dateProperty!.Value.TryGetProperty("format", out var format).Should().BeTrue("date property should have 'format'");
        format.GetString().Should().Be("date",
            "LocalDate? should be mapped to format 'date' by MapNodaTimeTypes()");
    }

    // ── Schema property checks ───────────────────────────────────────────────

    [Then(@"the swagger spec schema (.*) does not have property (.*)")]
    public void ThenSwaggerSchemaDoesNotHaveProperty(string schemaName, string propertyName)
    {
        var schema = GetSchema(schemaName);
        if (schema.TryGetProperty("properties", out var props))
        {
            props.TryGetProperty(propertyName, out _).Should().BeFalse(
                $"Schema {schemaName} should NOT have property '{propertyName}'");
        }
        // no "properties" key at all is also acceptable
    }

    [Then(@"the swagger spec schema (.*) allOf has property (.*)")]
    public void ThenSwaggerSchemaAllOfHasProperty(string schemaName, string propertyName)
    {
        var schema = GetSchema(schemaName);
        schema.TryGetProperty("allOf", out var allOf).Should().BeTrue(
            $"Schema {schemaName} should use allOf");

        var found = false;
        foreach (var allOfEntry in allOf.EnumerateArray())
        {
            if (allOfEntry.TryGetProperty("properties", out var props)
                && props.TryGetProperty(propertyName, out _))
            {
                found = true;
                break;
            }
        }

        found.Should().BeTrue($"Schema {schemaName} allOf should contain property '{propertyName}'");
    }

    // ── Non-OData endpoints must not have odata content types ─────────────────

    [Then(@"the swagger spec entity endpoint has no odata content type in responses")]
    public void ThenEntityEndpointHasNoODataContentType()
    {
        var paths = Doc.RootElement.GetProperty("paths");

        // Find any entity path (non-OData standard controller)
        var entityPath = paths.EnumerateObject()
            .FirstOrDefault(p => p.Name.Contains("/entity", StringComparison.OrdinalIgnoreCase)
                && !p.Name.Contains("odata", StringComparison.OrdinalIgnoreCase));

        entityPath.Value.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "should find an entity path in swagger");

        foreach (var verb in entityPath.Value.EnumerateObject())
        {
            if (!verb.Value.TryGetProperty("responses", out var responses)) continue;
            foreach (var response in responses.EnumerateObject())
            {
                if (!response.Value.TryGetProperty("content", out var content)) continue;
                foreach (var mediaType in content.EnumerateObject())
                {
                    mediaType.Name.Should().NotContain("odata",
                        $"Non-OData endpoint '{entityPath.Name}' should not have odata media type '{mediaType.Name}'");
                }
            }
        }
    }

    // ── FlaggedEnum rendered as array parameter ───────────────────────────────

    [Then(@"the swagger spec GET entity endpoint has EntityResult as array parameter")]
    public void ThenEntityGetEndpointHasEntityResultAsArray()
    {
        var paths = Doc.RootElement.GetProperty("paths");

        // Find GET /entity/{entityId} path (contains entityId in path template)
        var entityPath = paths.EnumerateObject()
            .FirstOrDefault(p => p.Name.Contains("/entity/", StringComparison.OrdinalIgnoreCase)
                && p.Name.Contains("entityId", StringComparison.OrdinalIgnoreCase));

        entityPath.Value.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "should find GET entity/{{entityId}} path in the swagger spec");

        entityPath.Value.TryGetProperty("get", out var getOp).Should().BeTrue("entity path should have GET operation");
        getOp.TryGetProperty("parameters", out var parameters).Should().BeTrue("GET operation should have parameters");

        var resultParam = parameters.EnumerateArray()
            .FirstOrDefault(p => p.TryGetProperty("name", out var n)
                && n.GetString()?.Equals("result", StringComparison.OrdinalIgnoreCase) == true);

        resultParam.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "should find 'result' parameter");

        resultParam.TryGetProperty("schema", out var schema).Should().BeTrue();
        schema.TryGetProperty("type", out var schemaType).Should().BeTrue();
        schemaType.GetString().Should().Be("array",
            "FlagsEnum parameter should be rendered as array by SupportFlaggedEnums filter");
    }

    // ── DefaultResponsesOperationFilter ───────────────────────────────────────

    [Then(@"the swagger spec entity GET endpoint has response (\d+)")]
    public void ThenEntityGetEndpointHasResponse(string statusCode)
    {
        var paths = Doc.RootElement.GetProperty("paths");

        // Match any entity get endpoint
        var entityGetOp = paths.EnumerateObject()
            .Where(p => p.Name.Contains("/entity", StringComparison.OrdinalIgnoreCase))
            .SelectMany(p => p.Value.EnumerateObject()
                .Where(v => v.Name.Equals("get", StringComparison.OrdinalIgnoreCase))
                .Select(v => v.Value))
            .FirstOrDefault(op => op.ValueKind != JsonValueKind.Undefined);

        entityGetOp.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "should find an entity GET operation");

        entityGetOp.TryGetProperty("responses", out var responses).Should().BeTrue();
        responses.TryGetProperty(statusCode, out _).Should().BeTrue(
            $"GET entity operation should have response {statusCode} (added by DefaultResponsesOperationFilter)");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private JsonElement GetSchema(string schemaName)
    {
        if (!Doc.RootElement.TryGetProperty("components", out var components))
            throw new InvalidOperationException("Swagger doc has no 'components' section");

        if (!components.TryGetProperty("schemas", out var schemas))
            throw new InvalidOperationException("Swagger doc has no 'components/schemas' section");

        if (!schemas.TryGetProperty(schemaName, out var schema))
        {
            var available = string.Join(", ", schemas.EnumerateObject().Select(p => p.Name));
            throw new InvalidOperationException($"Schema '{schemaName}' not found. Available: {available}");
        }

        return schema;
    }

    public void Dispose()
    {
        _swaggerDoc?.Dispose();
    }
}
