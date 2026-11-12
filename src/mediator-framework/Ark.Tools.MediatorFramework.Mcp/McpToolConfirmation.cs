// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core.BusinessRuleViolation;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>
/// Asks the user to confirm a tool call through MCP multi round-trip requests.
/// This is a user-experience aid, not a security control: a client can skip it,
/// send an accepted answer on the first call, or change the arguments on the retry.
/// </summary>
public static class McpToolConfirmation
{
    private const string _inputKey = "confirm";
    private const int _maxTextLength = 200;
    private const int _maxArgumentDepth = 32;

    /// <summary>
    /// Returns when the call is confirmed or the client cannot be asked.
    /// Otherwise requests the confirmation from the client, or fails the call when the user declined.
    /// </summary>
    /// <param name="context">The call-tool request context.</param>
    /// <param name="toolName">The tool name, for the error when the user declines.</param>
    /// <param name="message">The question shown to the user.</param>
    /// <param name="argumentNames">The arguments the tool declares; only these are shown to the user, below the question.</param>
    public static void EnsureConfirmed(
        RequestContext<CallToolRequestParams> context,
        string toolName,
        string message,
        params string[] argumentNames)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(toolName);
        ArgumentException.ThrowIfNullOrEmpty(message);
        ArgumentNullException.ThrowIfNull(argumentNames);

        if (context.Params?.InputResponses?.TryGetValue(_inputKey, out var response) is true)
        {
            if (!_isAccepted(response))
                throw new BusinessRuleViolationException(new BusinessRuleViolation("Not confirmed")
                {
                    // Mapped by McpToolErrors to an error result the agent can read.
                    Detail = "the user declined '" + toolName + "'. Do not retry without a new user request.",
                    Status = 409,
                });
            return;
        }

        var server = context.Server;
        // The question is a form-mode elicitation: a client that only opens URLs cannot answer it.
        if (!server.IsMrtrSupported || server.ClientCapabilities?.Elicitation?.Form is null)
            return;

        throw new InputRequiredException(
            new Dictionary<string, InputRequest>(StringComparer.Ordinal)
            {
                [_inputKey] = InputRequest.ForElicitation(new ElicitRequestParams
                {
                    Message = _message(message, argumentNames, context.Params?.Arguments, context.Services ?? server.Services),
                    RequestedSchema = new ElicitRequestParams.RequestSchema(),
                }),
            });
    }

    private static bool _isAccepted(InputResponse? response)
    {
        try
        {
            return response?.Deserialize(InputResponse.ElicitResultJsonTypeInfo)?.IsAccepted is true;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException)
        {
            // Not an elicitation result: the same as no confirmation.
            return false;
        }
    }

    private static string _message(string message, string[] argumentNames, IDictionary<string, JsonElement>? arguments, IServiceProvider? services)
    {
        var options = services is null ? null : McpToolSerializerOptions._getReadable(services);
        // The model writes the arguments. Only the declared ones are shown, in the declared order: the tool
        // ignores the others, so they must not push the real ones out of the user's view. A value can be long
        // (an attachment, for example), so each one is cut on its own.
        var json = new JsonObject();
        if (arguments is not null)
        {
            foreach (var name in argumentNames.Where(arguments.ContainsKey))
            {
                var node = _parse(arguments[name].GetRawText());
                // Measured on the compact form, so padding inside an object cannot push its content past the cut.
                var text = node?.ToJsonString(options) ?? "null";
                json[name] = text.Length <= _maxTextLength ? node : _cut(text);
            }
        }
        return json.Count == 0 ? message : message + "\n" + json.ToJsonString(options);
    }

    private static string _cut(string text)
    {
        // Not in the middle of a surrogate pair.
        var length = char.IsHighSurrogate(text[_maxTextLength - 1]) ? _maxTextLength - 1 : _maxTextLength;
        return string.Concat(text.AsSpan(0, length), "... (cut, ", (text.Length - length).ToString(CultureInfo.InvariantCulture), " more characters)");
    }

    private static JsonNode? _parse(string json)
    {
        try
        {
            return JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = _maxArgumentDepth });
        }
        catch (JsonException)
        {
            // Nested too deep to write inside the message: show the raw text.
            return json;
        }
    }
}
