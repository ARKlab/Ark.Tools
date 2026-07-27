// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Auth;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies deterministic NodaTime clock usage in the sample.</summary>
[TestClass]
public sealed class ClockParityTests
{
    /// <summary>Uses the injected fake clock for persisted greeting audit timestamps.</summary>
    [TestMethod]
    public async Task GreetingAuditUsesInjectedClock()
    {
        using var context = new SampleTestContext();
        context.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtTokenBuilder().AddSubject("test-user").AddScope(ApplicationScopes.GreetingWrite).Build());

        var create = await context.Client.PostAsJsonAsync("/api/v1/greetings", new { name = "clock" }).ConfigureAwait(false);
        create.EnsureSuccessStatusCode();

        var audits = await context.Client.GetFromJsonAsync<Ark.Tools.Core.PagedResult<AuditRecord>>(
            "/api/v1/audits?skip=0&limit=25",
            new JsonSerializerOptions().ConfigureArkDefaults()).ConfigureAwait(false);

        audits!.Data.Single(audit => audit.Operation == nameof(CreateGreetingRequest))
            .Timestamp.Should().Be(context.Clock.GetCurrentInstant());
    }
}
