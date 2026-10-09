// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;

using Microsoft.Extensions.DependencyInjection;

namespace Ark.MediatorFramework.Sample.Core.Functions.OutboxProcessor;

/// <summary>Composes the dedicated native messaging outbox processor.</summary>
public static class OutboxProcessorComposition
{
    /// <summary>Registers the outbox processor without a receive participant.</summary>
    /// <param name="services">The process service collection.</param>
    /// <param name="transport">The network transport.</param>
    /// <param name="contextFactory">The shared SQL or in-memory outbox context factory.</param>
    /// <param name="batchSize">The maximum number of messages processed per poll.</param>
    public static void AddOutboxProcessor(
        IServiceCollection services,
        IMessagingTransport transport,
        IOutboxAsyncContextFactory contextFactory,
        int batchSize = 10)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(contextFactory);
        services.AddSingleton(transport);
        services.AddArkMessagingOutboxProcessor(contextFactory, batchSize);
    }
}
