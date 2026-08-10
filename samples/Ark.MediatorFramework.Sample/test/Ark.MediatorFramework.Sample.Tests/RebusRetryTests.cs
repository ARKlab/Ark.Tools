// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.RebusProcessor;

using Ark.Tools.Rebus;

using Rebus.Bus;
using Rebus.Transport.InMem;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies second-level retry handling in the sample processor composition.</summary>
[TestClass]
public sealed class RebusRetryTests
{
    /// <summary>Ensures an exhausted Rebus request reaches the public error queue.</summary>
    [TestMethod]
    public async Task ExhaustedRequestMovesToErrorQueue()
    {
        var network = new InMemNetwork();
        await using var container = RebusProcessorComposition.BuildContainer(
            network,
            useSqlStore: false,
            registerHandlers: processorContainer =>
            {
                SampleRebusEndpoints.RegisterHandlers(processorContainer);
            },
            secondLevelRetriesEnabled: true);

        container.Verify();
        container.StartBus();
        await container.GetInstance<IBus>().Send(new FailingRebusRequest { Reason = "sample failure" }).ConfigureAwait(false);

        await WaitForQueueAsync(network, "error").ConfigureAwait(false);
        Assert.AreEqual(1, network.GetCount("error"));
    }

    private static async Task WaitForQueueAsync(InMemNetwork network, string queueName)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (network.GetCount(queueName) == 0)
            await Task.Delay(50, cts.Token).ConfigureAwait(false);
    }

}
