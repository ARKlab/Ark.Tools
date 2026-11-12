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
    /// Pass the application's options to the other overload to serialize the details as the HTTP API does.
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
    /// which are the application's own options.
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
            foreach (var extension in _serializable(mappedProblemDetails.Extensions, depth: 8, options))
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
        catch (Exception e) when (e is NotSupportedException or JsonException)
        {
            // A detail the options cannot write (a nested unknown type, a cycle): keep the error, drop the details.
            _logger.Warn(e, CultureInfo.InvariantCulture, "The MCP error details cannot be serialized and are left out.");
            problemDetails.Extensions.Clear();
            structuredContent = JsonSerializer.SerializeToElement(problemDetails, typeInfo);
        }
        // Many clients give the model only the text, so it carries the details too.
        if (problemDetails.Extensions.Count > 0)
            text += "\n" + _extensionsJson(structuredContent, problemDetails.Extensions.Keys);
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = text }],
            StructuredContent = structuredContent,
        };
    }

    // Options without reflection know a fixed set of types: a value of another type is left out, the others stay.
    // Dictionaries are copied, not changed: a nested one belongs to the business-rule violation. The depth limit
    // stops a dictionary that contains itself.
    private static Dictionary<string, object?> _serializable(IEnumerable<KeyValuePair<string, object?>> values, int depth, JsonSerializerOptions options)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var entry in values)
        {
            if (entry.Value is Dictionary<string, object?> nested && depth > 0)
                result[entry.Key] = _serializable(nested, depth - 1, options);
            else if (entry.Value is null || entry.Value is not Dictionary<string, object?> && options.TryGetTypeInfo(entry.Value.GetType(), out _))
                result[entry.Key] = entry.Value;
            else
                _logger.Warn(CultureInfo.InvariantCulture, "The MCP error detail {name} of type {type} cannot be serialized and is left out.", entry.Key, entry.Value.GetType());
        }
        return result;
    }

    private static string _extensionsJson(JsonElement problemDetails, ICollection<string> extensionKeys)
    {
        var extensions = new System.Text.Json.Nodes.JsonObject();
        foreach (var property in problemDetails.EnumerateObject().Where(property => extensionKeys.Contains(property.Name)))
            extensions[property.Name] = System.Text.Json.Nodes.JsonNode.Parse(property.Value.GetRawText());
        return extensions.ToJsonString();
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
                // The application's JSON options, so the details are written as the HTTP API writes them.
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
