// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
#if NET10_0_OR_GREATER
using Microsoft.AspNetCore.TestHost;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
#endif

namespace Ark.Tools.MediatorFramework.Tests;

[TestClass]
public sealed class MinimalApiHostingExtensionsTests
{
    [TestMethod]
    public void OpenApiConventionsReturnTheConfiguredOptions()
    {
        var options = new OpenApiOptions();

        var configured = options
            .AddArkTypeConverterValueSchemas()
            .AddArkNodaTimeSchemas()
            .AddArkPolymorphism<TestShape, TestShapeKind>(
                "kind",
                (TestShapeKind.Circle, typeof(TestCircle)));

        configured.Should().BeSameAs(options);
    }

#if NET10_0_OR_GREATER
    [TestMethod]
    public async Task TypeConverterWrapperBindsNodaTimeRouteAndNullableQueryValues()
    {
        Ark.Tools.Nodatime.NodaTimeConverter.Register();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOpenApi("v1", options => options
            .AddArkTypeConverterValueSchemas()
            .AddArkNodaTimeSchemas());
        await using var app = builder.Build();
        NodaTime.LocalDate boundDate = default;
        NodaTime.Instant? boundValue = null;
        app.MapGet("/instant/{date}", (
            [Microsoft.AspNetCore.Mvc.FromRoute(Name = "date")]
            ArkTypeConverterValue<NodaTime.LocalDate> date,
            [Microsoft.AspNetCore.Mvc.FromQuery]
            ArkTypeConverterValue<NodaTime.Instant?>? value) =>
        {
            boundDate = date.Value;
            boundValue = value?.Value;
            return TypedResults.Ok();
        });
        app.MapOpenApi();
        await app.StartAsync(app.Lifetime.ApplicationStarted);

        using var client = app.GetTestServer().CreateClient();
        using var missingResponse = await client.GetAsync(
            new Uri("http://localhost/instant/2026-07-24"),
            app.Lifetime.ApplicationStopping);
        missingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        boundDate.Should().Be(new NodaTime.LocalDate(2026, 7, 24));
        boundValue.Should().BeNull();

        using var validResponse = await client.GetAsync(
            new Uri("http://localhost/instant/2026-07-25?value=2026-07-24T10:15:30Z"),
            app.Lifetime.ApplicationStopping);
        validResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        boundDate.Should().Be(new NodaTime.LocalDate(2026, 7, 25));
        boundValue.Should().Be(NodaTime.Instant.FromUtc(2026, 7, 24, 10, 15, 30));

        using var invalidResponse = await client.GetAsync(
            new Uri("http://localhost/instant/invalid?value=invalid"),
            app.Lifetime.ApplicationStopping);
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await client.GetStringAsync(
            new Uri("http://localhost/openapi/v1.json"),
            app.Lifetime.ApplicationStopping));
        var parameters = document.RootElement.GetProperty("paths").GetProperty("/instant/{date}")
            .GetProperty("get").GetProperty("parameters");
        _assertParameterSchema(document.RootElement, parameters, "date", "string", "date");
        _assertParameterSchema(document.RootElement, parameters, "value", "string", "date-time");
    }

    [TestMethod]
    public async Task TypeConverterMetadataUsesWrappedClrType()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOpenApi("v1", options => options.AddArkTypeConverterValueSchemas());
        await using var app = builder.Build();
        app.MapGet("/converted", (
            [Microsoft.AspNetCore.Mvc.FromQuery]
            ArkTypeConverterValue<int> value) => TypedResults.Ok(value.Value));
        app.MapOpenApi();
        await app.StartAsync(app.Lifetime.ApplicationStarted);

        using var client = app.GetTestServer().CreateClient();
        using var response = await client.GetAsync(
            new Uri("http://localhost/converted?value=42"),
            app.Lifetime.ApplicationStopping);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await client.GetStringAsync(
            new Uri("http://localhost/openapi/v1.json"),
            app.Lifetime.ApplicationStopping));
        var parameters = document.RootElement.GetProperty("paths").GetProperty("/converted")
            .GetProperty("get").GetProperty("parameters");
        _assertParameterSchema(document.RootElement, parameters, "value", "integer", "int32");
    }

    [TestMethod]
    public async Task EnumsBindAsStringsFromRouteAndQuery()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddOpenApi("v1");
        await using var app = builder.Build();
        TestStatus routeValue = default;
        TestStatus queryValue = default;
        app.MapGet("/status/{routeValue}", (
            [Microsoft.AspNetCore.Mvc.FromRoute(Name = "routeValue")] TestStatus routeStatus,
            [Microsoft.AspNetCore.Mvc.FromQuery] TestStatus queryStatus) =>
        {
            routeValue = routeStatus;
            queryValue = queryStatus;
            return TypedResults.Ok();
        });
        app.MapOpenApi();
        await app.StartAsync(app.Lifetime.ApplicationStarted);

        using var client = app.GetTestServer().CreateClient();
        using var response = await client.GetAsync(
            new Uri("http://localhost/status/InProgress?queryStatus=Complete"),
            app.Lifetime.ApplicationStopping);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        routeValue.Should().Be(TestStatus.InProgress);
        queryValue.Should().Be(TestStatus.Complete);

        using var invalidResponse = await client.GetAsync(
            new Uri("http://localhost/status/unknown?queryStatus=Complete"),
            app.Lifetime.ApplicationStopping);
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await client.GetStringAsync(
            new Uri("http://localhost/openapi/v1.json"),
            app.Lifetime.ApplicationStopping));
        var parameters = document.RootElement.GetProperty("paths").GetProperty("/status/{routeValue}")
            .GetProperty("get").GetProperty("parameters");
        _assertParameterSchema(document.RootElement, parameters, "routeValue", "string", null);
        _assertParameterSchema(document.RootElement, parameters, "queryStatus", "string", null);
    }

    [TestMethod]
    public async Task OpenApiSecurityContainsPkceAndOpenIdConnectSchemes()
    {
        var settings = new ArkOpenApiSecuritySettings(
            new Uri("https://login.example.test/authorize"),
            new Uri("https://login.example.test/token"),
            new Uri("https://login.example.test/.well-known/openid-configuration"),
            "mediator-test",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["openid"] = "Sign in",
            });
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOpenApi("v1", options => options.AddArkOAuthSecurity(settings));
        await using var app = builder.Build();
        app.MapGet("/secured", () => "ok");
        app.MapOpenApi();
        await app.StartAsync(app.Lifetime.ApplicationStarted);

        using var client = app.GetTestServer().CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync(
            new Uri("http://localhost/openapi/v1.json"),
            app.Lifetime.ApplicationStopping));
        var components = document.RootElement.GetProperty("components").GetProperty("securitySchemes");
        components.GetProperty("oauth2").GetProperty("flows")
            .GetProperty("authorizationCode").GetProperty("authorizationUrl").GetString()
            .Should().Be("https://login.example.test/authorize");
        components.GetProperty("oauth2").GetProperty("flows")
            .GetProperty("authorizationCode").GetProperty("tokenUrl").GetString()
            .Should().Be("https://login.example.test/token");
        components.GetProperty("oidc").GetProperty("openIdConnectUrl").GetString()
            .Should().Be("https://login.example.test/.well-known/openid-configuration");
        document.RootElement.GetProperty("security")[0].GetProperty("oauth2")[0].GetString()
            .Should().Be("openid");
    }

    [TestMethod]
    public async Task OpenApiNodaTimeSchemasCoverNativeAndNullableTypes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOpenApi("v1", options => options.AddArkNodaTimeSchemas());
        await using var app = builder.Build();
        app.MapGet("/nodatime", () => new NodaTimeSchemaModel());
        app.MapOpenApi();
        await app.StartAsync(app.Lifetime.ApplicationStarted);

        using var client = app.GetTestServer().CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync(
            new Uri("http://localhost/openapi/v1.json"),
            app.Lifetime.ApplicationStopping));
        var components = document.RootElement.GetProperty("components").GetProperty("schemas");
        _assertSchema(components, "LocalDate", "date", "2016-01-21");
        _assertSchema(components, "LocalDateTime", "date-time", "2016-01-21T15:01:01.999999999");
        _assertSchema(components, "Instant", "date-time", "2016-01-21T15:01:01.999999999Z");
        _assertSchema(components, "OffsetDateTime", "date-time", "2016-01-21T15:01:01.999999999+02:00");
        _assertSchema(components, "ZonedDateTime", null, "2016-01-21T15:01:01.999999999+02:00 Europe/Rome");
        _assertSchema(components, "LocalTime", "time", "14:01:00.999999999");
        _assertSchema(components, "DateTimeZone", null, "Europe/Rome");
        _assertSchema(components, "Period", "duration", "P1Y2M-3DT4H");

        var nullable = components.GetProperty("NodaTimeSchemaModel")
            .GetProperty("properties").GetProperty("nullableLocalDate").GetProperty("oneOf");
        nullable.GetArrayLength().Should().Be(2);
        nullable[0].GetProperty("type").GetString().Should().Be("null");
        nullable[1].GetProperty("$ref").GetString().Should().Be("#/components/schemas/LocalDate");
    }
#endif

    private static void _assertSchema(
        JsonElement parent,
        string schemaName,
        string? format,
        string example)
    {
        var schema = parent.GetProperty(schemaName);
        schema.GetProperty("type").GetString().Should().Be("string");
        if (format is null)
            schema.TryGetProperty("format", out _).Should().BeFalse();
        else
            schema.GetProperty("format").GetString().Should().Be(format);

        schema.GetProperty("example").GetString().Should().Be(example);
    }

    private static JsonElement _resolveSchema(JsonElement document, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference))
            return schema;

        var referenceValue = reference.GetString()!;
        var componentName = referenceValue[(referenceValue.LastIndexOf('/', StringComparison.Ordinal) + 1)..];
        return document.GetProperty("components").GetProperty("schemas").GetProperty(componentName);
    }

    private static void _assertParameterSchema(
        JsonElement document,
        JsonElement parameters,
        string parameterName,
        string type,
        string? format)
    {
        var parameter = parameters.EnumerateArray().Single(item =>
            string.Equals(item.GetProperty("name").GetString(), parameterName, StringComparison.Ordinal));
        var schema = _resolveSchema(document, parameter.GetProperty("schema"));
        if (!schema.TryGetProperty("type", out var schemaType))
        {
            type.Should().Be("string");
            schema.GetProperty("enum").EnumerateArray()
                .Should().OnlyContain(item => item.ValueKind == JsonValueKind.String);
        }
        else if (schemaType.ValueKind == JsonValueKind.Array)
        {
            schemaType.EnumerateArray().Select(item => item.GetString()).Should().Contain(type);
        }
        else
        {
            schemaType.GetString().Should().Be(type);
        }
        if (format is null)
            schema.TryGetProperty("format", out _).Should().BeFalse();
        else
            schema.GetProperty("format").GetString().Should().Be(format);
    }

    [TestMethod]
    public void AttachmentMappingRegistersAnEndpoint()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        var route = app.MapArkAttachmentUpload<TestRequest, TestResponse>(
            "/uploads",
            attachment => new TestRequest { Attachment = attachment });

        route.Should().NotBeNull();
    }

    private enum TestShapeKind
    {
        Circle,
    }

    private enum TestStatus
    {
        Pending,
        InProgress,
        Complete,
    }

    private abstract record TestShape;

    private sealed record TestCircle : TestShape;

    private sealed record TestRequest : IRequest<TestResponse>
    {
        public required IArkAttachment Attachment { get; init; }
    }

    private sealed record TestResponse;

    private sealed record NodaTimeSchemaModel
    {
        public NodaTime.LocalDate LocalDate { get; init; }
        public NodaTime.LocalDateTime LocalDateTime { get; init; }
        public NodaTime.Instant Instant { get; init; }
        public NodaTime.OffsetDateTime OffsetDateTime { get; init; }
        public NodaTime.ZonedDateTime ZonedDateTime { get; init; }
        public NodaTime.LocalTime LocalTime { get; init; }
        public NodaTime.DateTimeZone DateTimeZone { get; init; } = NodaTime.DateTimeZone.Utc;
        public NodaTime.Period Period { get; init; } = NodaTime.Period.Zero;
        public NodaTime.LocalDate? NullableLocalDate { get; init; }
    }
}
