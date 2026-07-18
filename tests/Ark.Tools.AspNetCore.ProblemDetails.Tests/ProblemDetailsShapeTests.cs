// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
        Uri.TryCreate(problemDetails.Type!, UriKind.Absolute, out var typeUri).Should().BeTrue();
        typeUri!.IsAbsoluteUri.Should().BeTrue();
    }

    [TestMethod]
    public async Task SerializesProblemDetailsWithExpectedProperties()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-123",
        };
        httpContext.Response.Body = new MemoryStream();

        var handled = await new ArkProblemDetailsExceptionHandler()
            .TryHandleAsync(
                httpContext,
                new InvalidOperationException("secret exception detail"),
                CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        httpContext.Response.ContentType.Should().Be("application/problem+json");

        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(
            httpContext.Response.Body,
            cancellationToken: httpContext.RequestAborted);
        var root = document.RootElement;
        root.GetProperty("type").GetString().Should().Be("https://httpstatuses.com/500");
        root.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status500InternalServerError);
        root.GetProperty("traceId").GetString().Should().Be("trace-123");
        root.ToString().Should().NotContain("secret exception detail");
        root.TryGetProperty("title", out _).Should().BeFalse();
        root.TryGetProperty("detail", out _).Should().BeFalse();
        root.TryGetProperty("instance", out _).Should().BeFalse();
    }

    [TestMethod]
    public async Task IncludesExceptionDetailsInDevelopment()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-development",
        };
        httpContext.Response.Body = new MemoryStream();
        Exception exception;
        try
        {
            throw new InvalidOperationException("development exception detail");
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await new ArkProblemDetailsExceptionHandler(
                new TestHostEnvironment(Environments.Development),
                Options.Create(new ArkProblemDetailsOptions()))
            .TryHandleAsync(
                httpContext,
                exception,
                CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(
            httpContext.Response.Body,
            cancellationToken: httpContext.RequestAborted);
        var root = document.RootElement;
        root.GetProperty("detail").GetString().Should().Be("development exception detail");
        root.GetProperty("stackTrace").GetString().Should().Contain("IncludesExceptionDetailsInDevelopment");
    }

    [TestMethod]
    public async Task IncludesExceptionDetailsWhenExplicitlyEnabled()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await new ArkProblemDetailsExceptionHandler(
                options: Options.Create(new ArkProblemDetailsOptions { IncludeExceptionDetails = true }))
            .TryHandleAsync(
                httpContext,
                new InvalidOperationException("opt-in exception detail"),
                CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(
            httpContext.Response.Body,
            cancellationToken: httpContext.RequestAborted);
        document.RootElement.GetProperty("detail").GetString().Should().Be("opt-in exception detail");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; }
    }
}
