// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using System.Buffers;

namespace Ark.Tools.MediatorFramework.Tests;

[TestClass]
public sealed class InMemoryMessagingTransportPendingCountTests
{
    private static readonly IReadOnlyDictionary<string, string> _headers =
        new Dictionary<string, string>(StringComparer.Ordinal);

    [TestMethod]
    public void UnknownQueueHasNoPendingMessages()
    {
        var transport = new InMemoryMessagingTransport();

        transport.GetPendingCount("missing").Should().Be(0);
    }

    [TestMethod]
    public async Task PendingCountIncludesVisibleScheduledAndLockedDeliveries()
    {
        var transport = new InMemoryMessagingTransport();
        await transport.SendAsync("q", _headers, new ReadOnlySequence<byte>([1]), dueTime: null, default)
            .ConfigureAwait(false);
        await transport.SendAsync("q", _headers, new ReadOnlySequence<byte>([2]), DateTimeOffset.UtcNow.AddHours(1), default)
            .ConfigureAwait(false);

        transport.GetPendingCount("q").Should().Be(2, "one visible and one scheduled delivery are pending");

        var batch = await transport.ReceiveBatchAsync("q", 1, TimeSpan.Zero, default).ConfigureAwait(false);
        batch.Should().ContainSingle();
        transport.GetPendingCount("q").Should().Be(2, "the locked delivery and the scheduled delivery are pending");

        await batch[0].CompleteAsync(default).ConfigureAwait(false);
        transport.GetPendingCount("q").Should().Be(1, "only the scheduled delivery is pending");
    }
}
