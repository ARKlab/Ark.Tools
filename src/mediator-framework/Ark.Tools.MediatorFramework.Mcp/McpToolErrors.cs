// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.ProblemDetails;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NLog;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Maps mediator failures to safe MCP tool errors.</summary>
public static partial class McpToolErrors
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>Creates a call-tool filter that maps mediator failures to safe MCP results.</summary>
    /// <returns>The MCP call-tool error filter.</returns>
    public static McpRequestFilter<CallToolRequestParams, CallToolResult> CreateFilter()
    {
        return static next => _createHandler(next);
    }

    /// <summary>
    /// Creates an MCP tool result for a mediator failure.
    /// Client-visible failures use the shared ProblemDetails; unexpected failures
    /// use a generic message.
    /// </summary>
    /// <param name="exception">The mediator failure.</param>
    /// <returns>An MCP error result safe to return to a client.</returns>
    /// <remarks>
    /// This overload knows only strings, numbers, booleans, GUIDs, dates and lists of strings in the error details.
    /// Pass the application's options to the other overload to serialize the details with its converters and contexts.
    /// </remarks>
    public static CallToolResult ToToolResult(Exception exception)
    {
        return ToToolResult(exception, McpToolErrorJsonSerializerContext.Default.Options);
    }

    /// <summary>
    /// Creates an MCP tool result for a mediator failure, with the error details serialized by <paramref name="options"/>.
    /// Client-visible failures use the shared ProblemDetails; unexpected failures
    /// use a generic message.
    /// </summary>
    /// <param name="exception">The mediator failure.</param>
    /// <param name="options">
    /// The JSON options for the ProblemDetails and its details, normally <see cref="McpToolSerializerOptions.Get"/>,
    /// which are the application's own options. They must have metadata for
    /// <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/>.
    /// </param>
    /// <returns>An MCP error result safe to return to a client.</returns>
    public static CallToolResult ToToolResult(Exception exception, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
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
        var text = title + ": " + detail;
        var typeInfo = (JsonTypeInfo<Microsoft.AspNetCore.Mvc.ProblemDetails>)options.GetTypeInfo(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails));
        JsonElement structuredContent;
        try
        {
            structuredContent = JsonSerializer.SerializeToElement(problemDetails, typeInfo);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The details are mapped as they are, not filtered. If the options cannot write them (an unknown type,
            // a cycle, a converter that throws), the caller still gets the error, without the details.
            _logger.Warn(e, CultureInfo.InvariantCulture, "The MCP error details cannot be serialized and are left out.");
            problemDetails.Extensions.Clear();
            structuredContent = JsonSerializer.SerializeToElement(problemDetails, typeInfo);
        }
        // Many clients give the model only the text, so it carries the details too.
        if (problemDetails.Extensions.Count > 0)
            text += "\n" + _extensionsJson(structuredContent, problemDetails.Extensions.Keys, options.WriteIndented);
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = text }],
            StructuredContent = structuredContent,
        };
    }

    private static string _extensionsJson(JsonElement problemDetails, ICollection<string> extensionKeys, bool indented)
    {
        // The serialized text is reused as it is: a second parse has its own depth limit and can fail.
        return "{" + string.Join(",", problemDetails.EnumerateObject()
            .Where(property => extensionKeys.Contains(property.Name))
            // Indented text is written again on one line: the details must stay on the last line.
            .Select(property => "\"" + JsonEncodedText.Encode(property.Name) + "\":" + (indented
                ? JsonSerializer.Serialize(property.Value, McpToolErrorJsonSerializerContext.Default.JsonElement)
                : property.Value.GetRawText()))) + "}";
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
            catch (InputRequiredException)
            {
                // The SDK turns it into an input-required result (confirmation, see McpToolConfirmation).
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // The application's JSON options, so its converters and contexts apply to the details.
                var services = context.Services ?? context.Server.Services;
                return services is null ? ToToolResult(exception) : ToToolResult(exception, McpToolSerializerOptions.Get(services));
            }
        };
    }

    [JsonSourceGenerationOptions(
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true)]
    [JsonSerializable(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))]
    [JsonSerializable(typeof(Dictionary<string, string[]>))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(JsonElement))]
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(double))]
    [JsonSerializable(typeof(decimal))]
    [JsonSerializable(typeof(Guid))]
    [JsonSerializable(typeof(DateTime))]
    [JsonSerializable(typeof(DateTimeOffset))]
    [JsonSerializable(typeof(string[]))]
    [JsonSerializable(typeof(List<string>))]
    internal sealed partial class McpToolErrorJsonSerializerContext : JsonSerializerContext
    {
    }
}
