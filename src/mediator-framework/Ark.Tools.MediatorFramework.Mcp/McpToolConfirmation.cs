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

    /// <summary>
    /// Returns when the call is confirmed or the client cannot be asked.
    /// Otherwise requests the confirmation from the client, or fails the call when the user declined.
    /// </summary>
    /// <param name="context">The call-tool request context.</param>
    /// <param name="toolName">The tool name shown to the user.</param>
    /// <param name="description">The tool description shown to the user, if any.</param>
    /// <param name="argumentNames">The arguments the tool declares; only these are shown to the user.</param>
    public static void EnsureConfirmed(
        RequestContext<CallToolRequestParams> context,
        string toolName,
        string? description,
        params string[] argumentNames)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(toolName);
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
        if (!server.IsMrtrSupported || server.ClientCapabilities?.Elicitation is null)
            return;

        throw new InputRequiredException(
            new Dictionary<string, InputRequest>(StringComparer.Ordinal)
            {
                [_inputKey] = InputRequest.ForElicitation(new ElicitRequestParams
                {
                    Message = _message(toolName, description, argumentNames, context.Params?.Arguments),
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

    private static string _message(string toolName, string? description, string[] argumentNames, IDictionary<string, JsonElement>? arguments)
    {
        var message = "Confirm " + toolName;
        if (!string.IsNullOrEmpty(description))
            message += ": " + description;
        // The model writes the arguments. Only the declared ones are shown, in the declared order: the tool
        // ignores the others, so they must not push the real ones out of the user's view. A value can be long
        // (an attachment, for example), so each one is cut on its own.
        var json = new JsonObject();
        foreach (var name in argumentNames)
        {
            if (arguments?.TryGetValue(name, out var argument) is not true)
                continue;
            var value = argument.GetRawText();
            json[name] = value.Length <= _maxTextLength ? _parse(value) : _cut(value);
        }
        return json.Count == 0 ? message : message + "\nArguments: " + json.ToJsonString();
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
