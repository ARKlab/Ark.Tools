// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry.Trace;

using System.Diagnostics;

namespace Ark.Tools.OTel.Azure;

/// <summary>
/// Drops Azure Service Bus receive spans (<c>ServiceBusReceiver.*</c>, <c>Receive</c>) before
/// <see cref="ArkAdaptiveSampler"/> samples them, so polling receivers do not use its budget.
/// </summary>
public sealed class AzureServiceBusReceiveSamplingFilter : IArkSamplingFilter
{
    /// <inheritdoc/>
    public bool ShouldDrop(in SamplingParameters samplingParameters)
    {
        if (samplingParameters.Kind is not (ActivityKind.Client or ActivityKind.Consumer))
            return false;

        var name = samplingParameters.Name;
        if (name != null && name.StartsWith("ServiceBusReceiver.", StringComparison.OrdinalIgnoreCase))
            return true;

        string? messagingSystem = null, messagingOperation = null;
        if (samplingParameters.Tags != null)
        {
            foreach (var tag in samplingParameters.Tags)
            {
                switch (tag.Key)
                {
                    case "messaging.system":
                        messagingSystem = tag.Value as string;
                        break;
                    case "messaging.operation" or "messaging.operation.name":
                        messagingOperation ??= tag.Value as string;
                        break;
                }
            }
        }

        return string.Equals(messagingSystem, "servicebus", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(messagingOperation, "receive", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Receive", StringComparison.OrdinalIgnoreCase));
    }
}
