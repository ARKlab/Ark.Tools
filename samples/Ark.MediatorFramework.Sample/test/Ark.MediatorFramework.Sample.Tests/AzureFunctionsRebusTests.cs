// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.AzureFunctions;
using Ark.MediatorFramework.Sample.RebusProcessor;

using Ark.Tools.MediatorFramework.AzureFunctions.Generated;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Rebus;
using Ark.Tools.Rebus.Tests;
using Ark.Tools.Solid;

using Rebus.Activation;
using Rebus.Config;
using Rebus.Serialization.Json;
using Rebus.Transport.InMem;
using System.Text.Json;
using System.Buffers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies outbound-only Rebus composition without starting a receiver in the sender.</summary>
[TestClass]
public sealed class AzureFunctionsRebusTests
{
    /// <summary>Rejects a Function host without its required outbound bus configuration.</summary>
    [TestMethod]
    public void MissingOutboundBusConfigurationFailsClearly()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => AzureFunctionsRebusComposition.BuildContainer(null));

        StringAssert.Contains(
            exception.Message,
            "Azure Service Bus configuration is required",
            StringComparison.Ordinal);
    }

    /// <summary>Routes and delivers a typed message to an independently hosted receiver.</summary>
    [TestMethod]
    public async Task OutboundCompositionRoutesToOwnerQueue()
    {
        var network = new InMemNetwork();
        await using var sender = new Container();
        ApplicationComposition.Register(sender, useSqlStore: false);
        sender.RegisterInstance<IContextProvider<ClaimsPrincipal>>(new EmptyContextProvider());
        ApplicationComposition.RegisterOutboundRebus(
            sender,
            transport => transport.UseDrainableInMemoryTransportAsOneWayClient(network),
            SampleRebusEndpoints.ConfigureRouting);

        var received = new TaskCompletionSource<ProcessBookPrintProcessRequest>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var activator = new BuiltinHandlerActivator();
        activator.Handle<ProcessBookPrintProcessRequest>(message =>
        {
            received.SetResult(message);
            return Task.CompletedTask;
        });
        using var receiver = Configure.With(activator)
            .Transport(transport => transport.UseInMemoryTransport(network, "ark.mediator.sample"))
            .Serialization(serialization => serialization.UseSystemTextJson(
                new JsonSerializerOptions().ConfigureArkDefaults()))
            .Start();

        sender.Verify();
        sender.StartBus();
        await sender.GetInstance<Rebus.Bus.IBus>().Send(new ProcessBookPrintProcessRequest
        {
            Id = Guid.NewGuid(),
        }).ConfigureAwait(false);

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.AreNotEqual(Guid.Empty, message.Id);
    }

    /// <summary>Consumes the generated desired-resource manifest through startup reconciliation.</summary>
    [TestMethod]
    public async Task GeneratedMessagingResourcesAreReconciledAtStartup()
    {
        var transport = new InMemoryMessagingTransport();
        var services = new ServiceCollection();
        services.AddSingleton<IMessagingTransportManagement>(transport);
        services.AddArkMessagingResourceLifecycle(
            ArkGeneratedMessagingFunctions.Manifest.Resources);
        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(default)
            .ConfigureAwait(false);

        Assert.AreEqual(
            SampleMessagingParticipant.Identity,
            ArkGeneratedMessagingFunctions.Manifest.Resources.IdentityQueue,
            ignoreCase: false,
            CultureInfo.InvariantCulture);
        await transport.SendAsync(
            SampleMessagingParticipant.Identity,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new ReadOnlySequence<byte>(new byte[] { 1 }),
            null,
            default).ConfigureAwait(false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
#pragma warning disable MA0004 // The test disposes the enumerator at the end of the method.
        await using var enumerator = transport.ReceiveAsync(
            SampleMessagingParticipant.Identity,
            cts.Token).GetAsyncEnumerator(cts.Token);
#pragma warning restore MA0004
        Assert.IsTrue(await enumerator.MoveNextAsync().ConfigureAwait(false));
        await enumerator.Current.CompleteAsync(default).ConfigureAwait(false);
    }

    private sealed class EmptyContextProvider : IContextProvider<ClaimsPrincipal>
    {
        public ClaimsPrincipal Current => new(new ClaimsIdentity());
    }
}
