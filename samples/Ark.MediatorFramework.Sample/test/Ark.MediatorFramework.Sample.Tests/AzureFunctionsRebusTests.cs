// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.AzureFunctions;
using Ark.MediatorFramework.Sample.RebusProcessor;

using Ark.Tools.Rebus;
using Ark.Tools.Rebus.Tests;
using Ark.Tools.Solid;

using Rebus.Activation;
using Rebus.Config;
using Rebus.Serialization.Json;
using Rebus.Transport.InMem;
using System.Text.Json;

using SimpleInjector;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies outbound-only Rebus composition without starting a receiver in the sender.</summary>
[TestClass]
public sealed class AzureFunctionsRebusTests
{
    /// <summary>Verifies the shared anonymous readiness contract and handler.</summary>
    [TestMethod]
    public async Task HealthContractIsAvailableToFunctionHost()
    {
        var attribute = (Ark.MediatorFramework.HttpEndpointAttribute?)Attribute.GetCustomAttribute(
            typeof(HealthCheckQuery),
            typeof(Ark.MediatorFramework.HttpEndpointAttribute));

        Assert.IsNotNull(attribute);
        Assert.IsTrue(attribute.AllowAnonymous);
        Assert.AreEqual("GET", attribute.Verb);
        Assert.AreEqual("/health", attribute.Template);

        var response = await new HealthCheckHandler().ExecuteAsync(new HealthCheckQuery());
        Assert.AreEqual("ok", response.Status);
    }

    /// <summary>Rejects a Function host without its required outbound bus configuration.</summary>
    [TestMethod]
    public void MissingOutboundBusConfigurationFailsClearly()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => AzureFunctionsRebusComposition.BuildContainer(null));

        StringAssert.Contains(exception.Message, "Azure Service Bus configuration is required");
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

        var received = new TaskCompletionSource<CompleteGreetingCompositionRequest>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var activator = new BuiltinHandlerActivator();
        activator.Handle<CompleteGreetingCompositionRequest>(message =>
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
        await sender.GetInstance<Rebus.Bus.IBus>().Send(new CompleteGreetingCompositionRequest
        {
            Id = Guid.NewGuid(),
            Name = "outbound",
        }).ConfigureAwait(false);

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.AreEqual("outbound", message.Name);
    }

    private sealed class EmptyContextProvider : IContextProvider<ClaimsPrincipal>
    {
        public ClaimsPrincipal Current => new(new ClaimsIdentity());
    }
}
