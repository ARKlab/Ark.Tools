// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.ProblemDetails;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Maps mediator failures to safe MCP tool errors.</summary>
public static partial class McpToolErrors
{
    /// <summary>
    /// Creates an MCP tool result for a mediator failure.
    /// Client-visible failures use the shared ProblemDetails; unexpected failures
    /// use a generic message.
    /// </summary>
    /// <param name="exception">The mediator failure.</param>
    /// <returns>An MCP error result safe to return to a client.</returns>
    public static CallToolResult ToToolResult(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var mappedProblemDetails = ExceptionProblemDetailsMapper.Map(exception);
        var clientVisible = mappedProblemDetails.Status is >= 400 and < 500;
        var title = clientVisible
            ? mappedProblemDetails.Title ?? "An unexpected error occurred"
            : "An unexpected error occurred";
        var detail = clientVisible
            ? mappedProblemDetails.Detail ?? "The tool call could not be completed."
            : "The tool call could not be completed.";
        var problemDetails = clientVisible
            ? mappedProblemDetails
            : new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = mappedProblemDetails.Type,
                Status = mappedProblemDetails.Status,
                Title = title,
                Detail = detail,
            };
        var structuredContent = JsonSerializer.SerializeToElement(
            problemDetails,
            McpToolErrorJsonSerializerContext.Default.ProblemDetails);
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = title + ": " + detail }],
            StructuredContent = structuredContent,
        };
    }

    [JsonSerializable(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))]
    [JsonSerializable(typeof(Dictionary<string, string[]>))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    internal sealed partial class McpToolErrorJsonSerializerContext : JsonSerializerContext
    {
    }
}
