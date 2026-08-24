// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.ProblemDetails;

using Microsoft.AspNetCore.Http;

using NLog;

namespace Ark.Tools.MediatorFramework.AzureFunctions;

/// <summary>Creates HTTP results for generated Azure Functions endpoints.</summary>
public static class ArkAzureFunctionsResults
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>Reads the first HTTP ETag precondition.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The unquoted token, the wildcard, or <see langword="null"/>.</returns>
    public static string? ReadPrecondition(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ifMatch = context.Request.Headers.IfMatch.ToString();
        if (!string.IsNullOrWhiteSpace(ifMatch))
            return _unquote(ifMatch.Split(',')[0].Trim());
        return context.Request.Headers.IfNoneMatch.ToString().Trim() == "*" ? "*" : null;
    }

    /// <summary>Maps an application exception to a safe ProblemDetails result.</summary>
    /// <param name="exception">The exception to map.</param>
    /// <returns>A ProblemDetails result.</returns>
    public static IResult FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var problemDetails = ExceptionProblemDetailsMapper.Map(exception);
        if (problemDetails.Status is null or >= 500)
        {
            _logger.Error(exception, CultureInfo.InvariantCulture, "Unhandled exception while processing an Azure Functions request.");
        }
        return Results.Problem(
            statusCode: problemDetails.Status ?? StatusCodes.Status500InternalServerError,
            title: problemDetails.Title,
            type: problemDetails.Type,
            detail: problemDetails.Detail,
            extensions: problemDetails.Extensions);
    }

    /// <summary>Applies the response ETag and conditional GET behavior.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="token">The opaque handler-produced token.</param>
    /// <param name="conditionalGet">Whether to evaluate <c>If-None-Match</c>.</param>
    /// <returns>A 304 result when the token matches; otherwise <see langword="null"/>.</returns>
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
            .Select(value => value.StartsWith("W/", StringComparison.Ordinal) ? value[2..] : value)
            .Select(_unquote)
            .Any(value => value == "*" || string.Equals(value, token, StringComparison.Ordinal));
        return matches ? TypedResults.StatusCode(StatusCodes.Status304NotModified) : null;
    }

    /// <summary>Determines whether a token can safely be emitted as an HTTP header value.</summary>
    /// <param name="value">The token to validate.</param>
    /// <returns><see langword="true"/> when the token is safe.</returns>
    public static bool IsValidToken(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.All(character => character != '"' && character != '\\'
            && character >= '\u0020' && character != '\u007f');
    }

    private static string _unquote(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
}
