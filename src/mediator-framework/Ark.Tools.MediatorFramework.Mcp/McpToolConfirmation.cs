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
    private const int _maxValueLength = 200;

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

        var json = new JsonObject();
        foreach (var argument in arguments)
        {
            // The model writes the arguments, so a value can be long (an attachment, for example).
            // Each value is cut on its own, so a long one cannot hide the others from the user.
            var value = argument.Value.GetRawText();
            json[argument.Key] = value.Length <= _maxValueLength
                ? JsonNode.Parse(value)
                : string.Concat(value.AsSpan(0, _maxValueLength), "... (cut, ", (value.Length - _maxValueLength).ToString(CultureInfo.InvariantCulture), " more characters)");
        }
        return message + "\nArguments: " + json.ToJsonString();
    }
}
