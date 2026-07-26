// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Http;

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>Reads and validates HTTP opaque ETag preconditions.</summary>
public static class ArkETag
{
    /// <summary>
    /// Reads the first <c>If-Match</c> value, or <c>*</c> from <c>If-None-Match</c> when present.
    /// Only the first comma-separated <c>If-Match</c> entry is honored.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The unquoted precondition, or <see langword="null"/> when none was supplied.</returns>
    public static string? ReadPrecondition(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ifMatch = context.Request.Headers.IfMatch.ToString();
        if (!string.IsNullOrWhiteSpace(ifMatch))
            return Unquote(ifMatch.Split(',')[0].Trim());

        return string.Equals(context.Request.Headers.IfNoneMatch.ToString().Trim(), "*", StringComparison.Ordinal)
            ? "*"
            : null;
    }

    /// <summary>Returns whether a token is safe to emit in an HTTP header.</summary>
    /// <param name="value">The token to validate.</param>
    /// <returns><see langword="true"/> when the token contains no header-injection characters.</returns>
    public static bool IsValidToken(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.All(character => character != '"' && character != '\\'
            && character >= '\u0020' && character != '\u007f');
    }

    private static string Unquote(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;
}

/// <summary>Marks a generated endpoint as carrying an ETag precondition parameter.</summary>
public sealed class ArkETagParameterMetadata
{
}
