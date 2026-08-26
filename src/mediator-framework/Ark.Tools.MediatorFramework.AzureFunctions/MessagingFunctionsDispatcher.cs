// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using Azure.Messaging.ServiceBus;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.MediatorFramework.AzureFunctions;

/// <summary>Maps Azure Functions Service Bus bindings onto the transport-neutral dispatcher.</summary>
public static class MessagingFunctionsDispatcher
{
    /// <summary>Dispatches one Service Bus PeekLock delivery with explicit settlement.</summary>
    /// <param name="message">The received Service Bus message.</param>
    /// <param name="messageActions">The Functions manual-settlement actions.</param>
    /// <param name="functionContext">The current Functions invocation context.</param>
    /// <param name="cancellationToken">The host cancellation token.</param>
    /// <returns>A task that completes after dispatch and settlement.</returns>
    public static async Task DispatchAsync(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        FunctionContext functionContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(messageActions);
        ArgumentNullException.ThrowIfNull(functionContext);

        var dispatcher = functionContext.InstanceServices.GetRequiredService<MessagingDispatcher>();
        await dispatcher.OnDeliveryAsync(
            new ServiceBusMessagingLockedDelivery(message, messageActions),
            cancellationToken).ConfigureAwait(false);
    }
}
