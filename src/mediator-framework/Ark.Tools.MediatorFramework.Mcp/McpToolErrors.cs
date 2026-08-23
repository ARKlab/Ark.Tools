// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.ProblemDetails;
using ModelContextProtocol;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Maps mediator failures to safe MCP tool errors.</summary>
public static partial class McpToolErrors
{
    /// <summary>
    /// Creates an MCP exception for a mediator failure.
    /// Client-visible 4xx failures use the shared ProblemDetails JSON; other failures
    /// use a generic message.
    /// </summary>
    /// <param name="exception">The mediator failure.</param>
    /// <returns>An MCP exception safe to return to a client.</returns>
    public static McpException ToMcpException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var problemDetails = ExceptionProblemDetailsMapper.Map(exception);
        if (problemDetails.Status is >= 400 and < 500)
        {
            var json = JsonSerializer.Serialize(
                problemDetails,
                McpToolErrorJsonSerializerContext.Default.ProblemDetails);
            return new McpException(json, exception);
        }

        return new McpException("An unexpected error occurred.", exception);
    }

    [JsonSerializable(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))]
    [JsonSerializable(typeof(Dictionary<string, string[]>))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    internal sealed partial class McpToolErrorJsonSerializerContext : JsonSerializerContext
    {
    }
}
