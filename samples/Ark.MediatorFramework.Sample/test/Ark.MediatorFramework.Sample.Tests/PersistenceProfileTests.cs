// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using Ark.Tools.Core;
using Ark.Tools.Outbox;

using AwesomeAssertions;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies persistence-sensitive application contracts in the selected profile.</summary>
[TestClass]
public sealed class PersistenceProfileTests
{
    /// <summary>Verifies round-trip, paging, audit, and opaque ETag behavior.</summary>
    [TestMethod]
    [TestCategory("persistence")]
    public async Task GreetingPersistenceSupportsRoundTripPagingAuditsAndOpaqueEtags()
    {
        await DatabaseHooks.ResetDatabaseAsync().ConfigureAwait(false);
        await using var context = new ApplicationTestContext();
        context.SetAuthenticatedUser("profile-user");

        var first = await context.DispatchRequestAsync<Greeting_CreateRequest.V1, Greeting.V1.Output>(
            new Greeting_CreateRequest.V1(new Greeting.V1.Create { Name = "Persistence alpha" })).ConfigureAwait(false);
        var second = await context.DispatchRequestAsync<Greeting_CreateRequest.V1, Greeting.V1.Output>(
            new Greeting_CreateRequest.V1(new Greeting.V1.Create { Name = "Persistence beta" })).ConfigureAwait(false);
        await context.DispatchRequestAsync<Greeting_CreateRequest.V1, Greeting.V1.Output>(
            new Greeting_CreateRequest.V1(new Greeting.V1.Create { Name = "Other greeting" })).ConfigureAwait(false);

        first.ETag.Should().StartWith("0x");
        first.ETag.Length.Should().Be(18);

        var queried = await context.DispatchQueryAsync<GetGreetingQuery, GreetingResponse>(
            new GetGreetingQuery { Id = first.Id }).ConfigureAwait(false);
        queried.Message.Should().Be(first.Message);
        queried.ETag.Should().Be(first.ETag);

        var updated = await context.DispatchRequestAsync<Greeting_UpdateRequest.V1, Greeting.V1.Output>(
            new Greeting_UpdateRequest.V1(
                new Greeting.V1.Input { Message = "Updated Persistence greeting" },
                first.Id,
                first.ETag)).ConfigureAwait(false);
        updated.ETag.Should().StartWith("0x");
        updated.ETag.Length.Should().Be(18);
        updated.ETag.Should().NotBe(first.ETag);

        var page = await context.DispatchQueryAsync<SearchGreetingsQuery, GreetingPage>(
            new SearchGreetingsQuery
            {
                MessageContains = "Persistence",
                Skip = 1,
                Limit = 1,
            }).ConfigureAwait(false);
        page.Count.Should().Be(2);
        page.Data.Should().HaveCount(1);
        page.Data[0].Message.Should().Contain("Persistence");

        var audits = await context.DispatchQueryAsync<GetAuditsQuery, PagedResult<AuditRecord>>(
            new GetAuditsQuery
            {
                Identifier = first.Id.ToString("D"),
                Limit = 25,
            }).ConfigureAwait(false);
        audits.Count.Should().Be(2);
        audits.Data.Should().Contain(record => record.Operation == $"{typeof(Greeting_CreateRequest).Name}.{typeof(Greeting_CreateRequest.V1).Name}");
        audits.Data.Should().Contain(record => record.Operation == $"{typeof(Greeting_UpdateRequest).Name}.{typeof(Greeting_UpdateRequest.V1).Name}");
        audits.Data.Should().OnlyContain(record => record.UserId == "profile-user");
    }

    /// <summary>Verifies that both profiles commit application outbox messages transactionally.</summary>
    [TestMethod]
    [TestCategory("persistence")]
    public async Task ProfileOutboxCommitsMessagesTransactionally()
    {
        await DatabaseHooks.ResetDatabaseAsync().ConfigureAwait(false);
        await using var context = new ApplicationTestContext();
        await using var dataContext = await context.CreateDataContextAsync().ConfigureAwait(false);
        await dataContext.OutboxContext.SendAsync(
            [
                new OutboxMessage
                {
                    Headers = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["type"] = nameof(CompleteGreetingCompositionRequest),
                    },
                    Body = [1, 2, 3],
                },
            ]).ConfigureAwait(false);

        (await dataContext.OutboxContext.CountAsync().ConfigureAwait(false)).Should().Be(1);
        await dataContext.CommitAsync().ConfigureAwait(false);
        (await context.GetOutboxCountAsync().ConfigureAwait(false)).Should().Be(1);

        await context.ClearOutboxAsync().ConfigureAwait(false);
        (await context.GetOutboxCountAsync().ConfigureAwait(false)).Should().Be(0);
    }
}
