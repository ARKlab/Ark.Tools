// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry.Trace;

using System.Diagnostics;

namespace Ark.Tools.OTel.Azure;

/// <summary>
/// Azure SDK rules for <see cref="ArkAdaptiveSamplerOptions.Filter"/>.
/// </summary>
public static class AzureSamplingFilters
{
    /// <summary>
    /// Returns <see langword="true"/> for an Azure Service Bus receive span (<c>ServiceBusReceiver.*</c>,
    /// <c>Receive</c>), which polling receivers create at a high rate.
    /// </summary>
    /// <param name="samplingParameters">The name, kind and creation tags of the span.</param>
    /// <returns><see langword="true"/> to drop the span.</returns>
    public static bool IsServiceBusReceive(in SamplingParameters samplingParameters)
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
