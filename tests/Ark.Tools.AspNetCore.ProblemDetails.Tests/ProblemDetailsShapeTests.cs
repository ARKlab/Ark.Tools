// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.ProblemDetails;
using Ark.Tools.Authorization;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using System.Text.Json;

namespace Ark.Tools.AspNetCore.ProblemDetails.Tests;

/// <summary>Verifies the RFC 7807 shape emitted by the ProblemDetails library.</summary>
[TestClass]
public sealed class ProblemDetailsShapeTests
{
    [TestMethod]
    public void MapsExceptionWithTheSameUriTypeAsTheExistingImplementation()
    {
        var problemDetails = ExceptionProblemDetailsMapper.Map(new EntityNotFoundException());

        problemDetails.Status.Should().Be(StatusCodes.Status404NotFound);
        problemDetails.Type.Should().Be("https://httpstatuses.com/404");
        Uri.TryCreate(problemDetails.Type, UriKind.Absolute, out var typeUri).Should().BeTrue();
        typeUri.IsAbsoluteUri.Should().BeTrue();
    }

    [TestMethod]
    public void SerializesProblemDetailsWithExpectedProperties()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-123",
        };
        httpContext.Response.Body = new MemoryStream();

        var handled = new ArkProblemDetailsExceptionHandler()
            .TryHandleAsync(httpContext, new InvalidOperationException(), CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        httpContext.Response.ContentType.Should().Be("application/problem+json");

        httpContext.Response.Body.Position = 0;
        using var document = JsonDocument.Parse(httpContext.Response.Body);
        var root = document.RootElement;
        root.GetProperty("type").GetString().Should().Be("https://httpstatuses.com/500");
        root.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status500InternalServerError);
        root.GetProperty("traceId").GetString().Should().Be("trace-123");
        root.TryGetProperty("title", out _).Should().BeFalse();
        root.TryGetProperty("detail", out _).Should().BeFalse();
        root.TryGetProperty("instance", out _).Should().BeFalse();
    }
}
