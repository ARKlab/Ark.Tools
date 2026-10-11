// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Rebus;
using Ark.Tools.Outbox;

using AwesomeAssertions;

using Rebus.Activation;
using Rebus.Config;
using Rebus.Transport.InMem;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies that the Rebus adapter enlists only when its transport has an outbox.</summary>
[TestClass]
public sealed class RebusMessagingBusOutboxTests
{
    [TestMethod]
    public async Task EnlistWithoutOutboxFailsNamingTheParticipant()
    {
        using var activator = new BuiltinHandlerActivator();
        var bus = new RebusMessagingBus(_startBus(activator), "sender", [], outboxEnabled: false);
        var context = await new InMemoryOutboxContextFactory().CreateAsync().ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);

        var enlist = () => bus.Enlist(context);

        enlist.Should().Throw<InvalidOperationException>()
            .WithMessage("Rebus participant 'sender' has no outbox*Outbox(...)*outboxEnabled: true*");
    }

    [TestMethod]
    public async Task EnlistWithOutboxOpensATransactionScope()
    {
        using var activator = new BuiltinHandlerActivator();
        var bus = new RebusMessagingBus(_startBus(activator), "sender", [], outboxEnabled: true);
        var context = await new InMemoryOutboxContextFactory().CreateAsync().ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);

        using var scope = bus.Enlist(context);

        global::Rebus.Transport.AmbientTransactionContext.Current.Should().NotBeNull();
    }

    private static global::Rebus.Bus.IBus _startBus(BuiltinHandlerActivator activator)
    {
        return Configure.With(activator)
            .Logging(static logging => logging.None())
            .Transport(static transport => transport.UseInMemoryTransport(new InMemNetwork(), "rebus-outbox-tests"))
            .Start();
    }
}
