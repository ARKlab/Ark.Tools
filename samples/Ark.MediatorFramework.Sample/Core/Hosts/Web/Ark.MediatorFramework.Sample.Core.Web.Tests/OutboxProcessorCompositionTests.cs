// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Web.Hosting;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ark.MediatorFramework.Sample.Core.Web.Tests;

[TestClass]
public sealed class OutboxProcessorCompositionTests
{
    [TestMethod]
    public async Task IsolatedHostRunsExactlyOneReservedProcessor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOutboxProcessorHost(
            static _ => new InMemoryMessagingTransport(),
            static _ => new InMemoryOutboxContextFactory());
        var provider = services.BuildServiceProvider();
        await using var __provider = provider.ConfigureAwait(false);
        var host = provider.GetServices<IHostedService>().Should().ContainSingle().Which.Should().BeOfType<OutboxProcessorHost>().Subject;

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            host.Services.GetServices<IHostedService>().Should()
                .ContainSingle(static service => service is MessagingOutboxProcessor);
            MessagingOutboxProcessor.Identity.Should().Be("outbox-processor");
        }
        finally
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
