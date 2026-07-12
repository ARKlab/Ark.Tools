// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;

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
}
