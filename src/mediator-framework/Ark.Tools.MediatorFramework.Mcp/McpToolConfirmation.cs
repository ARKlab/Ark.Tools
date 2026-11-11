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
    private const int _maxArguments = 20;

    /// <summary>
    /// Returns when the call is confirmed or the client cannot be asked.
    /// Otherwise requests the confirmation from the client, or fails the call when the user declined.
    /// </summary>
    /// <param name="context">The call-tool request context.</param>
    /// <param name="toolName">The tool name shown to the user.</param>
    /// <param name="description">The tool description shown to the user, if any.</param>
    public static void EnsureConfirmed(RequestContext<CallToolRequestParams> context, string toolName, string? description)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(toolName);

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
        if (!server.IsMrtrSupported || server.ClientCapabilities?.Elicitation is null)
            return;

        throw new InputRequiredException(
            new Dictionary<string, InputRequest>(StringComparer.Ordinal)
            {
                [_inputKey] = InputRequest.ForElicitation(new ElicitRequestParams
                {
                    Message = _message(toolName, description, context.Params?.Arguments),
                    RequestedSchema = new ElicitRequestParams.RequestSchema(),
                }),
            },
            // Opaque request state for the client to send back; not read on the second call.
            _inputKey);
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

    private static string _message(string toolName, string? description, IDictionary<string, JsonElement>? arguments)
    {
        var message = "Confirm " + toolName;
        if (!string.IsNullOrEmpty(description))
            message += ": " + description;
        if (arguments is not { Count: > 0 })
            return message;

        // The model writes the arguments, so a name or a value can be long (an attachment, for example) and
        // there can be many. Each one is cut on its own and the count is limited, so that the user sees the first ones.
        var json = new JsonObject();
        foreach (var argument in arguments.Take(_maxArguments))
        {
            var value = argument.Value.GetRawText();
            json[_cut(argument.Key)] = value.Length <= _maxTextLength ? _parse(value) : _cut(value);
        }
        if (arguments.Count > _maxArguments)
            json["..."] = (arguments.Count - _maxArguments).ToString(CultureInfo.InvariantCulture) + " more arguments";
        return message + "\nArguments: " + json.ToJsonString();
    }

    private static string _cut(string text)
    {
        if (text.Length <= _maxTextLength)
            return text;
        // Not in the middle of a surrogate pair.
        var length = char.IsHighSurrogate(text[_maxTextLength - 1]) ? _maxTextLength - 1 : _maxTextLength;
        return string.Concat(text.AsSpan(0, length), "... (cut, ", (text.Length - length).ToString(CultureInfo.InvariantCulture), " more characters)");
    }

    private static JsonNode? _parse(string json)
    {
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            // Nested deeper than the parser allows: show the raw text.
            return json;
        }
    }
}
