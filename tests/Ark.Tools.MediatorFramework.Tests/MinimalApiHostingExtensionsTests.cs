// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
#if NET10_0_OR_GREATER
using Microsoft.AspNetCore.TestHost;
using System.Text.Json;
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
            .AddArkNodaTimeSchemas()
            .AddArkPolymorphism<TestShape, TestShapeKind>(
                "kind",
                (TestShapeKind.Circle, typeof(TestCircle)));

        configured.Should().BeSameAs(options);
    }

#if NET10_0_OR_GREATER
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
        AssertSchema(components, "LocalDate", "date", "2016-01-21");
        AssertSchema(components, "LocalDateTime", "date-time", "2016-01-21T15:01:01.999999999");
        AssertSchema(components, "Instant", "date-time", "2016-01-21T15:01:01.999999999Z");
        AssertSchema(components, "OffsetDateTime", "date-time", "2016-01-21T15:01:01.999999999+02:00");
        AssertSchema(components, "ZonedDateTime", null, "2016-01-21T15:01:01.999999999+02:00 Europe/Rome");
        AssertSchema(components, "LocalTime", "time", "14:01:00.999999999");
        AssertSchema(components, "DateTimeZone", null, "Europe/Rome");
        AssertSchema(components, "Period", "duration", "P1Y2M-3DT4H");

        var nullable = components.GetProperty("NodaTimeSchemaModel")
            .GetProperty("properties").GetProperty("nullableLocalDate").GetProperty("oneOf");
        nullable.GetArrayLength().Should().Be(2);
        nullable[0].GetProperty("type").GetString().Should().Be("null");
        nullable[1].GetProperty("$ref").GetString().Should().Be("#/components/schemas/LocalDate");
    }
#endif

    private static void AssertSchema(
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
