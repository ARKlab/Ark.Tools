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

        // Only the receive itself: settlement and lock renewal (ServiceBusReceiver.Complete, .RenewMessageLock, ...)
        // run inside message processing, and the application calls peek and deferred receives explicitly.
        var name = samplingParameters.Name;
        if (string.Equals(name, "ServiceBusReceiver.Receive", StringComparison.OrdinalIgnoreCase))
            return true;

        // Older Service Bus instrumentation names the span "Receive".
        if (!string.Equals(name, "Receive", StringComparison.OrdinalIgnoreCase) || samplingParameters.Tags is null)
            return false;

        foreach (var tag in samplingParameters.Tags)
        {
            if (tag.Key == "messaging.system")
                return string.Equals(tag.Value as string, "servicebus", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
