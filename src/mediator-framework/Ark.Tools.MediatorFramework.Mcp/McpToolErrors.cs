// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.ProblemDetails;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NLog;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Maps mediator failures to safe MCP tool errors.</summary>
public static partial class McpToolErrors
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>Creates a call-tool filter that maps mediator failures to safe MCP results.</summary>
    /// <returns>The MCP call-tool error filter.</returns>
    public static McpRequestFilter<CallToolRequestParams, CallToolResult> CreateFilter()
    {
        return next => _createHandler(next);
    }

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
        if (!clientVisible)
            _logger.Error(exception, CultureInfo.InvariantCulture, "Unhandled exception while processing an MCP tool call.");
        var title = clientVisible
            ? mappedProblemDetails.Title ?? "An unexpected error occurred"
            : "An unexpected error occurred";
        var detail = clientVisible
            ? mappedProblemDetails.Detail ?? "The tool call could not be completed."
            : "The tool call could not be completed.";
        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = clientVisible ? mappedProblemDetails.Type : null,
            Status = clientVisible ? mappedProblemDetails.Status : 500,
            Title = title,
            Detail = detail,
        };
        if (clientVisible)
        {
            foreach (var extension in mappedProblemDetails.Extensions)
            {
                problemDetails.Extensions[extension.Key] = extension.Value;
            }
        }
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

    private static McpRequestHandler<CallToolRequestParams, CallToolResult> _createHandler(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        return async (context, cancellationToken) =>
        {
            try
            {
                return await next(context, cancellationToken).ConfigureAwait(false);
            }
            catch (McpException)
            {
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return ToToolResult(exception);
            }
        };
    }

    [JsonSerializable(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))]
    [JsonSerializable(typeof(Dictionary<string, string[]>))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    internal sealed partial class McpToolErrorJsonSerializerContext : JsonSerializerContext
    {
    }
}
