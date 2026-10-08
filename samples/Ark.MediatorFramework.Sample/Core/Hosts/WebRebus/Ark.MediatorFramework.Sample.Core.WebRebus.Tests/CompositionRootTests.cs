// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Rebus;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;

using Rebus.Handlers;

namespace Ark.MediatorFramework.Sample.Core.WebRebus.Tests;

/// <summary>Verifies the WebRebus composition roots.</summary>
[TestClass]
public sealed class CompositionRootTests
{
    /// <summary>The WebInterface starts, verifies its container, starts its bus, and exposes health.</summary>
    [TestMethod]
    public async Task WebInterfaceStartsAndExposesHealth()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);

        host.Container.IsLocked.Should().BeTrue();
        host.Container.GetInstance<Ark.Tools.MediatorFramework.IBus>().Should().BeOfType<RebusMessagingBus>();
        using var response = await host.App.GetTestClient().GetAsync(
            new Uri("/healthCheck", UriKind.Relative),
            host.App.Lifetime.ApplicationStopping).ConfigureAwait(false);
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    /// <summary>The api is a one-way client: it receives nothing and hosts no subscriber.</summary>
    [TestMethod]
    public async Task WebInterfaceRegistersNoRebusHandlerOrSubscriber()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);

        host.Container.GetRegistration<IHandleMessages<CreateBookReviewRequest.V1>>(throwOnFailure: false)
            .Should().BeNull();
        host.Container.GetRegistration<ICommandHandler<BookPrintCompleted>>(throwOnFailure: false)
            .Should().BeNull();
    }
}
