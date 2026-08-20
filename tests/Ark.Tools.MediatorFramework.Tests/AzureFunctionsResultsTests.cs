// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.AzureFunctions;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

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
}
