// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AotSample.Core.API;
using Ark.MediatorFramework.AotSample.Core.Web.WebInterface;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using System.Text.Json;

namespace Ark.MediatorFramework.AotSample.Core.Web.Tests;

/// <summary>
/// Resolves the JSON metadata of every endpoint and message without SQL Server: reflection-based JSON is off in this
/// project, as in the published apps, so a type missing from <c>AotJsonContext</c> fails here.
/// </summary>
[TestClass]
public sealed class JsonMetadataTests
{
    [TestMethod]
    public async Task EndpointAndMessageTypesResolveFromTheSourceGeneratedContext()
    {
        JsonSerializer.IsReflectionEnabledByDefault.Should().BeFalse();
        // Composition only: nothing connects to the database.
        var builder = WebInterfaceApp.CreateBuilder(["--ConnectionStrings:Sql=Server=unused"]);
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        await using var __app = app.ConfigureAwait(false);
        WebInterfaceApp.Configure(app);
        var http = app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
        var messaging = app.Services.GetRequiredService<IOptions<JsonSerializerOptions>>().Value;
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(static source => source.Endpoints).ToList();
        var types = endpoints
            .SelectMany(static endpoint => endpoint.Metadata.GetOrderedMetadata<IAcceptsMetadata>())
            .Select(static accepts => accepts.RequestType)
            .Concat(endpoints
                .SelectMany(static endpoint => endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>())
                .Select(static produces => produces.Type))
            .OfType<Type>()
            .Where(static type => type != typeof(void))
            .Distinct()
            .ToList();

        types.Should().Contain(typeof(Book));
        foreach (var type in types)
            http.GetTypeInfo(type).Should().NotBeNull();
        messaging.GetTypeInfo(typeof(PrintBookCommand)).Should().NotBeNull();
    }
}
