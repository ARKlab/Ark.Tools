// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.MediatorFramework.AzureFunctions;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using System.Text.Json;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies Azure Functions HTTP result parity helpers.</summary>
[TestClass]
public sealed class AzureFunctionsResultsTests
{
    [TestMethod]
    public void AppliesStrongETagAndConditionalGet()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfNoneMatch = "\"abc\"";

        var result = ArkAzureFunctionsResults.ApplyResponseETag(context, "abc", true);

        result.Should().NotBeNull();
        context.Response.Headers.ETag.ToString().Should().Be("\"abc\"");
    }

    [TestMethod]
    public void ReadsFirstIfMatchPrecondition()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = "\"abc\", \"other\"";

        ArkAzureFunctionsResults.ReadPrecondition(context).Should().Be("abc");
    }

    [TestMethod]
    public void RejectsUnsafeETag()
    {
        ArkAzureFunctionsResults.IsValidToken("a\"b").Should().BeFalse();
        ArkAzureFunctionsResults.IsValidToken("a\\b").Should().BeFalse();
        ArkAzureFunctionsResults.IsValidToken("a\nb").Should().BeFalse();
    }

    [TestMethod]
    public async Task WritesExceptionsWithTheSharedNamingRules()
    {
        var services = new ServiceCollection();
        services.ConfigureHttpJsonOptions(static options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        });
        var provider = services.BuildServiceProvider();
        await using var __provider = provider.ConfigureAwait(false);
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();

        await ArkAzureFunctionsResults.FromException(new BusinessRuleViolationException(new NamedViolation()))
            .ExecuteAsync(context).ConfigureAwait(false);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: context.RequestAborted)
            .ConfigureAwait(false);
        var detail = document.RootElement.GetProperty("business_rule_violation");
        detail.GetProperty("max_amount").GetInt32().Should().Be(5);
        detail.GetProperty("data").GetProperty("KeepMe").GetInt32().Should().Be(1);
    }

    [TestMethod]
    public async Task HidesExceptionDetailsOutsideDevelopment()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        await using var __provider = provider.ConfigureAwait(false);
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();

        await ArkAzureFunctionsResults.FromException(new InvalidOperationException("secret"))
            .ExecuteAsync(context).ConfigureAwait(false);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Be("application/problem+json");
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
        body.Should().Contain("traceId").And.NotContain("secret").And.NotContain("stackTrace");
    }

    private sealed class NamedViolation() : BusinessRuleViolation("Named")
    {
        public int MaxAmount { get; } = 5;

        public Dictionary<string, int> Data { get; } = new(StringComparer.Ordinal) { ["KeepMe"] = 1 };
    }
}
