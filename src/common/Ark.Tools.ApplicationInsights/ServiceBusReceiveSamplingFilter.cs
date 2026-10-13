// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry.Trace;

using System.Diagnostics;

namespace Ark.Tools.ApplicationInsights;

// Copy of Ark.Tools.OTel.Azure.AzureSamplingFilters.IsServiceBusReceive: referencing that package would bring its
// Azure SDK switch and dependencies to this compatibility package, which is removed in v8.
internal static class ServiceBusReceiveSamplingFilter
{
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
