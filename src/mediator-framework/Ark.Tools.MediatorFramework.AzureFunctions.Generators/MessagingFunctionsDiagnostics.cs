// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.CodeAnalysis;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Generators;

/// <summary>The diagnostics reported by the Azure Functions messaging generator.</summary>
internal static class MessagingFunctionsDiagnostics
{
    /// <summary>An Azure Functions app binds more than one messaging participant.</summary>
    public static readonly DiagnosticDescriptor _multipleHosts = _rule(
        "ARKMF033",
        "Multiple Functions messaging hosts",
        "An Azure Functions app can bind exactly one messaging participant",
        DiagnosticSeverity.Error);

    /// <summary>The bound type is not a messaging participant.</summary>
    public static readonly DiagnosticDescriptor _invalidParticipant = _rule(
        "ARKMF034",
        "Invalid Functions messaging participant",
        "Type '{0}' is not marked with MessagingParticipant",
        DiagnosticSeverity.Error);

    /// <summary>The bound participant is not listed by a messaging network.</summary>
    public static readonly DiagnosticDescriptor _missingNetwork = _rule(
        "ARKMF035",
        "Functions messaging participant has no network",
        "Participant '{0}' is not listed by a messaging network",
        DiagnosticSeverity.Error);

    /// <summary>The bound participant is listed by more than one messaging network.</summary>
    public static readonly DiagnosticDescriptor _multipleNetworks = _rule(
        "ARKMF036",
        "Functions messaging participant has multiple networks",
        "Participant '{0}' is listed by more than one messaging network",
        DiagnosticSeverity.Error);

    /// <summary>The bound participant consumes no contracts.</summary>
    public static readonly DiagnosticDescriptor _senderOnly = _rule(
        "ARKMF037",
        "Functions messaging participant is sender-only",
        "Participant '{0}' consumes no contracts, so no receive trigger is generated",
        DiagnosticSeverity.Info);

    /// <summary>The requested trigger binding is not implemented.</summary>
    public static readonly DiagnosticDescriptor _unsupportedBinding = _rule(
        "ARKMF038",
        "Functions messaging trigger binding is not implemented",
        "Trigger binding value '{0}' is not supported by this generator version",
        DiagnosticSeverity.Error);

    /// <summary>A subscribed event has no single publisher.</summary>
    public static readonly DiagnosticDescriptor _invalidSubscription = _rule(
        "ARKMF039",
        "Functions messaging subscription has no publisher",
        "Subscribed event '{0}' does not have exactly one publisher in network '{1}'",
        DiagnosticSeverity.Error);

    /// <summary>The subscriber cannot deserialize the publisher protocol.</summary>
    public static readonly DiagnosticDescriptor _serializerMismatch = _rule(
        "ARKMF045",
        "Functions messaging subscriber cannot deserialize publisher protocol",
        "Participant '{0}' does not support effective protocol '{1}' published by '{2}' for event '{3}'",
        DiagnosticSeverity.Error);

    /// <summary>Storage Queue host settings cannot be inspected.</summary>
    public static readonly DiagnosticDescriptor _hostJsonNotInspectable = _rule(
        "ARKMF040",
        "Storage Queue host settings are not inspectable",
        "Add host.json to AdditionalFiles so Storage Queue messaging settings can be validated",
        DiagnosticSeverity.Info);

    /// <summary>The Storage Queue message encoding is invalid.</summary>
    public static readonly DiagnosticDescriptor _invalidMessageEncoding = _rule(
        "ARKMF041",
        "Invalid Storage Queue message encoding",
        "host.json extensions.queues.messageEncoding must be the literal 'none'",
        DiagnosticSeverity.Warning);

    /// <summary>The Storage Queue maximum dequeue count is invalid.</summary>
    public static readonly DiagnosticDescriptor _invalidMaximumDequeueCount = _rule(
        "ARKMF042",
        "Invalid Storage Queue maximum dequeue count",
        "host.json extensions.queues.maxDequeueCount must be a positive integer",
        DiagnosticSeverity.Warning);

    /// <summary>The Storage Queue visibility timeout is invalid.</summary>
    public static readonly DiagnosticDescriptor _invalidVisibilityTimeout = _rule(
        "ARKMF043",
        "Invalid Storage Queue visibility timeout",
        "host.json extensions.queues.visibilityTimeout must be a positive TimeSpan",
        DiagnosticSeverity.Warning);

    /// <summary>A Storage Queue consumer declares no retry policy.</summary>
    public static readonly DiagnosticDescriptor _missingStorageQueueRetry = _rule(
        "ARKMF044",
        "Storage Queue consumer has no retry policy",
        "Storage Queue participant '{0}' must declare a retry policy with a positive RetryDelay",
        DiagnosticSeverity.Error);

    /// <summary>Two logical messaging names map to the same native entity name.</summary>
    public static readonly DiagnosticDescriptor _nativeNameCollision = _rule(
        "ARKMF046",
        "Messaging native entity name collision",
        "Logical messaging names '{0}' and '{1}' map to the same {2} entity name '{3}'",
        DiagnosticSeverity.Error);

    private static DiagnosticDescriptor _rule(
        string id,
        string title,
        string message,
        DiagnosticSeverity severity)
    {
        return new DiagnosticDescriptor(
            id,
            title,
            message,
            "Ark.Tools.MediatorFramework",
            severity,
            isEnabledByDefault: true,
            helpLinkUri: $"https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/{id}.md");
    }
}
