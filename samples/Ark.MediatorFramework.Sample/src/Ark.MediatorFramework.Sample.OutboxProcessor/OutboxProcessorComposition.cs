// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;

using Microsoft.Extensions.DependencyInjection;

namespace Ark.MediatorFramework.Sample.OutboxProcessor;

/// <summary>Composes the dedicated native messaging outbox processor.</summary>
public static class OutboxProcessorComposition
{
    /// <summary>Builds the processor service provider without a receive participant.</summary>
    /// <param name="transport">The network transport.</param>
    /// <param name="contextFactory">The shared SQL or in-memory outbox context factory.</param>
    /// <param name="batchSize">The maximum number of messages processed per poll.</param>
    /// <returns>The processor service provider.</returns>
    public static ServiceProvider BuildServices(
        IMessagingTransport transport,
        IOutboxAsyncContextFactory contextFactory,
        int batchSize = 10)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(contextFactory);
        var services = new ServiceCollection();
        services.AddSingleton(transport);
        services.AddSingleton<IMessagingTransport>(transport);
        services.AddArkMessagingOutboxProcessor(contextFactory, batchSize);
        return services.BuildServiceProvider();
    }
}
