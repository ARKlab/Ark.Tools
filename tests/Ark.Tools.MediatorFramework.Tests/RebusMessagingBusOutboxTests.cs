// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Rebus;
using Ark.Tools.Outbox;

using AwesomeAssertions;

using Rebus.Activation;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.Transport.InMem;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies that the Rebus adapter stages the sends made in an <see cref="OutboxScope"/>.</summary>
[TestClass]
public sealed class RebusMessagingBusOutboxTests
{
    [TestMethod]
    public async Task SendInScopeWithoutOutboxFailsNamingTheParticipant()
    {
        using var activator = new BuiltinHandlerActivator();
        var bus = new RebusMessagingBus(_startBus(activator, new InMemNetwork(), outbox: false), "sender", [], outboxEnabled: false);
        var context = await new InMemoryOutboxContextFactory().CreateAsync().ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);

        using var scope = new OutboxScope(context);
        var send = async () => await bus.Send("message").ConfigureAwait(false);

        await send.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Rebus participant 'sender' has no outbox*Outbox(...)*outboxEnabled: true*").ConfigureAwait(false);
    }

    [TestMethod]
    public async Task SendInScopeWithOutboxIsStoredOnCompletion()
    {
        var network = new InMemNetwork();
        using var activator = new BuiltinHandlerActivator();
        var bus = new RebusMessagingBus(_startBus(activator, network, outbox: true), "sender", [], outboxEnabled: true);
        var factory = new InMemoryOutboxContextFactory();
        var context = await factory.CreateAsync().ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);

        using (var scope = new OutboxScope(context))
        {
            await bus.Send("message").ConfigureAwait(false);
            await scope.CompleteAsync().ConfigureAwait(false);
        }
        await context.CommitAsync().ConfigureAwait(false);

        network.Count(_queue).Should().Be(0);
        var inspection = await factory.CreateAsync().ConfigureAwait(false);
        await using var __inspection = inspection.ConfigureAwait(false);
        var messages = (await inspection.PeekLockMessagesAsync().ConfigureAwait(false)).ToList();
        messages.Should().ContainSingle()
            .Which.Headers.Should().Contain("rbs2-outbox-recipient", _queue);
    }

    private const string _queue = "rebus-outbox-tests";

    private static global::Rebus.Bus.IBus _startBus(BuiltinHandlerActivator activator, InMemNetwork network, bool outbox)
    {
        return Configure.With(activator)
            .Logging(static logging => logging.None())
            .Transport(transport =>
            {
                transport.UseInMemoryTransport(network, _queue);
                if (outbox)
                    transport.Outbox(static o => o.OutboxOptions(static options => options.StartProcessor = false));
            })
            .Routing(static routing => routing.TypeBased().Map<string>(_queue))
            .Start();
    }
}
