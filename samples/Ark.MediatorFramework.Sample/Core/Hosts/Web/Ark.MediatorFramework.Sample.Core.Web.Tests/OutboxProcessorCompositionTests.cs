// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Web.OutboxProcessor;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ark.MediatorFramework.Sample.Core.Web.Tests;

/// <summary>Verifies the dedicated outbox processor host composition.</summary>
[TestClass]
public sealed class OutboxProcessorCompositionTests
{
    [TestMethod]
    public async Task DedicatedHostResolvesExactlyOneReservedProcessor()
    {
        var services = new ServiceCollection();
        OutboxProcessorComposition.AddOutboxProcessor(
            services,
            new InMemoryMessagingTransport(),
            new InMemoryOutboxContextFactory());
        var provider = services.BuildServiceProvider();
        await using var __provider = provider.ConfigureAwait(false);

        provider.GetServices<IHostedService>().Should()
            .ContainSingle(static service => service is MessagingOutboxProcessor);
        MessagingOutboxProcessor.Identity.Should().Be("outbox-processor");
    }
}
