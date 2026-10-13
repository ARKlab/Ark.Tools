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
    /// Returns <see langword="true"/> for an Azure Service Bus receive span (<c>ServiceBusReceiver.Receive</c>, or
    /// <c>Receive</c> from older instrumentation), which polling receivers create at a high rate.
    /// </summary>
    /// <param name="samplingParameters">The name, kind and creation tags of the span.</param>
    /// <returns><see langword="true"/> to drop the span.</returns>
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
