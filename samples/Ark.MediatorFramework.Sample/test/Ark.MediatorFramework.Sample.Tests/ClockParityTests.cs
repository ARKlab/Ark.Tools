// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;
using Ark.Tools.Core;

using AwesomeAssertions;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies deterministic clock usage through application contracts.</summary>
[TestClass]
public sealed class ClockParityTests
{
    /// <summary>Uses the injected fake clock for persisted greeting audit timestamps.</summary>
    [TestMethod]
    public async Task GreetingAuditUsesInjectedClock()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        context.SetAuthenticatedUser("clock-user");

        var greeting = await context.DispatchRequestAsync<Greeting_CreateRequest.V1, Greeting.V1.Output>(
            new Greeting_CreateRequest.V1(new Greeting.V1.Create { Name = "clock" })).ConfigureAwait(false);
        var audits = await context.DispatchQueryAsync<GetAuditsQuery, PagedResult<AuditRecord>>(
            new GetAuditsQuery
            {
                Identifier = greeting.Id.ToString("D"),
                Limit = 25,
            }).ConfigureAwait(false);

        audits.Data.Single(audit => audit.Operation == $"{typeof(Greeting_CreateRequest).Name}.{typeof(Greeting_CreateRequest.V1).Name}")
            .Timestamp.Should().Be(context.Clock.GetCurrentInstant());
    }
}
