// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;
using FluentValidation;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies paging and validation through application contracts.</summary>
[TestClass]
public sealed class PagingTests
{
    /// <summary>Returns pages with a stable total count and disjoint results.</summary>
    [TestMethod]
    public async Task SearchReturnsPagesAndTotalCount()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        context.SetAuthenticatedUser("paging-user");
        await CreateAsync(context, "page-one").ConfigureAwait(false);
        await CreateAsync(context, "page-two").ConfigureAwait(false);
        await CreateAsync(context, "page-three").ConfigureAwait(false);

        var first = await SearchAsync(context, 0, 2).ConfigureAwait(false);
        var second = await SearchAsync(context, 2, 2).ConfigureAwait(false);

        first.Count.Should().Be(3);
        first.Data.Should().HaveCount(2);
        second.Count.Should().Be(3);
        second.Data.Should().HaveCount(1);
        first.Data.Select(greeting => greeting.Id).Should().NotContain(second.Data[0].Id);
    }

    /// <summary>Reports validation failures for invalid paging values.</summary>
    [TestMethod]
    public async Task SearchRejectsInvalidPaging()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        var action = () => context.DispatchQueryAsync<SearchGreetingsQuery, GreetingPage>(
            new SearchGreetingsQuery { Skip = -1, Limit = 0 });

        var exception = await action.Should().ThrowAsync<ValidationException>().ConfigureAwait(false);
        exception.Which.Errors.Should().Contain(error => error.PropertyName == nameof(SearchGreetingsQuery.Skip));
        exception.Which.Errors.Should().Contain(error => error.PropertyName == nameof(SearchGreetingsQuery.Limit));
    }

    private static async Task CreateAsync(ApplicationTestContext context, string name)
    {
        await context.DispatchRequestAsync<CreateGreetingRequest, GreetingResponse>(
            new CreateGreetingRequest { Name = name }).ConfigureAwait(false);
    }

    private static async Task<GreetingPage> SearchAsync(ApplicationTestContext context, int skip, int limit)
    {
        return await context.DispatchQueryAsync<SearchGreetingsQuery, GreetingPage>(
            new SearchGreetingsQuery { Skip = skip, Limit = limit }).ConfigureAwait(false);
    }
}
