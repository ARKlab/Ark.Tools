// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies streaming and cancellation through application contracts.</summary>
[TestClass]
public sealed class AsyncEnumerableStreamingTests
{
    /// <summary>Enumerates items in order and observes cancellation.</summary>
    [TestMethod]
    public async Task DirectStreamDeliversItemsAndSupportsCancellation()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        context.SetAuthenticatedUser("stream-user");
        using var cancellation = new CancellationTokenSource();
        var stream = await context.DispatchQueryAsync<GetGreetingsStreamQuery, IAsyncEnumerable<GreetingStreamItem>>(
            new GetGreetingsStreamQuery { Count = 100, DelayMilliseconds = 0 },
            cancellation.Token).ConfigureAwait(false);
        var items = new List<GreetingStreamItem>();

        var action = async () =>
        {
            await foreach (var item in stream.WithCancellation(cancellation.Token).ConfigureAwait(false))
            {
                items.Add(item);
                if (items.Count == 2)
                    await cancellation.CancelAsync().ConfigureAwait(false);
            }
        };

        await action.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
        items.Select(item => item.Index).Should().Equal(0, 1);
        items[0].Message.Should().Be("Hello, stream item 0!");
    }

    /// <summary>Returns no items when the application query requests an empty stream.</summary>
    [TestMethod]
    public async Task DirectEmptyStreamIsEmpty()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        context.SetAuthenticatedUser("stream-user");
        var stream = await context.DispatchQueryAsync<GetGreetingsStreamQuery, IAsyncEnumerable<GreetingStreamItem>>(
            new GetGreetingsStreamQuery { Count = 0 }).ConfigureAwait(false);

        var items = await stream.ToListAsync().ConfigureAwait(false);

        items.Should().BeEmpty();
    }
}
