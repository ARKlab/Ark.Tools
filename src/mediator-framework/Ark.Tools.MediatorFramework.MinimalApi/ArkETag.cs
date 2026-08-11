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
            return _unquote(ifMatch.Split(',')[0].Trim());

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

    /// <summary>Applies a handler-produced ETag and handles a matching conditional GET.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="token">The opaque response token.</param>
    /// <param name="conditionalGet">
    /// Whether <c>If-None-Match</c> should be evaluated for this response.
    /// </param>
    /// <returns>A 304 result when the request matches; otherwise <see langword="null"/>.</returns>
    public static IResult? ApplyResponseETag(HttpContext context, string? token, bool conditionalGet)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrEmpty(token))
            return null;

        if (!IsValidToken(token))
        {
            var position = token.Select((character, index) => (character, index))
                .First(item => item.character == '"' || item.character == '\\'
                    || item.character < '\u0020' || item.character == '\u007f')
                .index;
            throw new InvalidOperationException($"ETag token contains an invalid character at position {position}.");
        }

        context.Response.Headers.ETag = "\"" + token + "\"";
        if (!conditionalGet)
            return null;

        var matches = context.Request.Headers.IfNoneMatch
            .ToString()
            .Split(',', StringSplitOptions.TrimEntries)
            .Select(value => value.StartsWith("W/", StringComparison.Ordinal)
                ? _unquote(value[2..])
                : _unquote(value))
            .Any(value => value == "*" || string.Equals(value, token, StringComparison.Ordinal));
        return matches ? TypedResults.StatusCode(StatusCodes.Status304NotModified) : null;
    }

    private static string _unquote(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;
}

/// <summary>Marks a generated endpoint as carrying an ETag precondition parameter.</summary>
public sealed class ArkETagParameterMetadata
{
    /// <summary>Initializes metadata describing ETag request and response behavior.</summary>
    /// <param name="requestETag">Whether the request carries an ETag precondition.</param>
    /// <param name="responseETag">Whether the response carries an ETag.</param>
    public ArkETagParameterMetadata(bool requestETag = false, bool responseETag = false)
    {
        RequestETag = requestETag;
        ResponseETag = responseETag;
    }

    /// <summary>Gets whether the request carries an ETag precondition.</summary>
    public bool RequestETag { get; }

    /// <summary>Gets whether the response carries an ETag.</summary>
    public bool ResponseETag { get; }
}
