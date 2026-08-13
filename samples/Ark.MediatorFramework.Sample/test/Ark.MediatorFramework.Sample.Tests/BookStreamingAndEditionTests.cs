// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies Book streaming and polymorphic edition contracts.</summary>
[TestClass]
public sealed class BookStreamingAndEditionTests
{
    /// <summary>Rejects a stream request above the application safety bound.</summary>
    [TestMethod]
    public async Task StreamRejectsCountAboveBound()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        context.SetAuthenticatedUser("stream-user");

        var action = () => context.DispatchQueryAsync<StreamBooksQuery, IAsyncEnumerable<BookStreamItem>>(
            new StreamBooksQuery { Count = 101 });

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>().ConfigureAwait(false);
    }

    /// <summary>Returns the concrete edition and its computed description.</summary>
    [TestMethod]
    public async Task DescribeEditionDispatchesConcreteVariant()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        context.SetAuthenticatedUser("edition-user");

        var result = await context.DispatchRequestAsync<DescribeBookEditionRequest, BookEditionDescription>(
            new DescribeBookEditionRequest
            {
                Edition = new DigitalBookEdition
                {
                    Format = "EPUB",
                    SizeBytes = 1_048_576,
                },
            }).ConfigureAwait(false);

        result.Edition.Should().BeOfType<DigitalBookEdition>();
        result.Description.Should().Be("EPUB digital edition with 1048576 bytes");
    }
}
