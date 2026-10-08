// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.OutboxProcessor;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies the dedicated outbox processor host composition.</summary>
[TestClass]
public sealed class OutboxProcessorCompositionTests
{
    [TestMethod]
    public async Task DedicatedHostResolvesExactlyOneReservedProcessor()
    {
        var transport = new InMemoryMessagingTransport();
        var factory = new InMemoryOutboxContextFactory();
        var services = OutboxProcessorComposition.BuildServices(transport, factory);
        await using var __services = services.ConfigureAwait(false);

        services.GetServices<IHostedService>().Should()
            .ContainSingle(static service => service is MessagingOutboxProcessor);
        MessagingOutboxProcessor.Identity.Should().Be("outbox-processor");
    }
}
