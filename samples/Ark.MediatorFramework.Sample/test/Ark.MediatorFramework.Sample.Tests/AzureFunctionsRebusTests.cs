// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.AzureFunctions;
using Ark.MediatorFramework.Sample.WebInterface;

using Ark.Tools.MediatorFramework.AzureFunctions;
using Ark.Tools.MediatorFramework.AzureFunctions.Generated;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Rebus;
using Ark.Tools.Rebus.Tests;
using Ark.Tools.Solid;
using Ark.Tools.MediatorFramework.AzureFunctions.SimpleInjector;
using Ark.Tools.Solid.SimpleInjector;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Serialization.Json;
using Rebus.Transport.InMem;
using NodaTime;
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
    /// <summary>Uses the native Functions composition without registering a Rebus bus.</summary>
    [TestMethod]
    public async Task NativeCompositionDoesNotRegisterRebus()
    {
        await using var container = AzureFunctionsNativeComposition.BuildContainer();

        Assert.IsNull(container.GetRegistration<Rebus.Bus.IBus>(throwOnFailure: false));
        Assert.IsNotNull(container.GetRegistration<ICommandHandler<ProcessBookPrintProcessRequest>>(
            throwOnFailure: false));
        Assert.IsNotNull(
            container.GetRegistration<ICommandHandler<MessagingFailed<ProcessBookPrintProcessRequest>>>(
                throwOnFailure: false));
    }

    /// <summary>Rejects a Function host without its required outbound bus configuration.</summary>
    [TestMethod]
    public void MissingOutboundBusConfigurationFailsClearly()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            static () => AzureFunctionsRebusComposition.BuildContainer(null));

        StringAssert.Contains(
            exception.Message,
            "Azure Service Bus configuration is required",
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Adds the optional outbound bus without removing the native application
    /// handlers or registering a Rebus receiver.
    /// </summary>
    [TestMethod]
    public async Task OptionalOutboundCompositionPreservesNativeComposition()
    {
        await using var container = AzureFunctionsNativeComposition.BuildContainer();

        AzureFunctionsRebusComposition.ConfigureOutbound(
            container,
            "sb://sample.servicebus.windows.net/");

        Assert.IsNotNull(container.GetRegistration<Rebus.Bus.IBus>(throwOnFailure: false));
        Assert.IsNotNull(container.GetRegistration<ICommandHandler<ProcessBookPrintProcessRequest>>(
            throwOnFailure: false));
        Assert.IsNull(
            container.GetRegistration<IHandleMessages<ProcessBookPrintProcessRequest>>(
                throwOnFailure: false));
    }

    /// <summary>Routes and delivers a typed message to an independently hosted receiver.</summary>
    [TestMethod]
    public async Task OutboundCompositionRoutesToOwnerQueue()
    {
        var network = new InMemNetwork();
        await using var sender = new Container();
        ApplicationComposition.Register(sender, useSqlStore: false);
        sender.RegisterInstance<IContextProvider<ClaimsPrincipal>>(new EmptyContextProvider());
        var rebusRequirements = SampleRebusHost.GetRequirements();
        SampleRebusHost.Register(
            (serviceType, implementationType) => sender.Collection.Append(serviceType, implementationType));
        sender.RegisterSingleton<Ark.Tools.MediatorFramework.Rebus.RebusMessagingBus>(() =>
            new Ark.Tools.MediatorFramework.Rebus.RebusMessagingBus(
                sender.GetInstance<global::Rebus.Bus.IBus>(),
                rebusRequirements.Identity,
                rebusRequirements.PublishedEventTypes));
        sender.RegisterSingleton<Ark.Tools.MediatorFramework.IBus>(
            () => sender.GetInstance<Ark.Tools.MediatorFramework.Rebus.RebusMessagingBus>());
        sender.RegisterSingleton<Ark.Tools.MediatorFramework.IBusOutboxEnlistment>(
            () => sender.GetInstance<Ark.Tools.MediatorFramework.Rebus.RebusMessagingBus>());
        ApplicationComposition.RegisterOutboundRebus(
            sender,
            transport => transport.UseDrainableInMemoryTransportAsOneWayClient(network),
            SampleRebusHost.ConfigureRouting);

        var received = new TaskCompletionSource<ProcessBookPrintProcessRequest>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var activator = new BuiltinHandlerActivator();
        activator.Handle<ProcessBookPrintProcessRequest>(message =>
        {
            received.SetResult(message);
            return Task.CompletedTask;
        });
        using var receiver = Configure.With(activator)
            .Transport(transport => transport.UseInMemoryTransport(network, "ark-mediator-sample"))
            .Serialization(static serialization => serialization.UseSystemTextJson(
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
        services._addArkMessagingResourceLifecycle(
            ArkGeneratedMessagingFunctions.Manifest.Resources);
        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IHostedService>().StartAsync(default)
            .ConfigureAwait(false);

        Assert.AreEqual(
            SampleMessagingNotificationParticipant.Identity,
            ArkGeneratedMessagingFunctions.Manifest.Resources.IdentityQueue,
            ignoreCase: false,
            CultureInfo.InvariantCulture);
        await transport.SendAsync(
            SampleMessagingParticipant.Identity,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new ReadOnlySequence<byte>(new byte[] { 1 }),
            null,
            default).ConfigureAwait(false);
        var delivery = await transport.ReceiveOneAsync(SampleMessagingParticipant.Identity)
            .ConfigureAwait(false);
        await delivery.CompleteAsync(default).ConfigureAwait(false);
    }

    /// <summary>Bridges the native Functions application container into Microsoft DI for scoped processor dispatch.</summary>
    [TestMethod]
    public async Task NativeFunctionsCompositionBridgesScopedCommandProcessingIntoMicrosoftDependencyInjection()
    {
        await using var container = AzureFunctionsNativeComposition.BuildContainer(useSqlStore: false);
        var recorder = new BridgeExecutionRecorder();
        container.Register<BridgeScopeMarker>(Lifestyle.Scoped);
        container.RegisterInstance(recorder);
        container.Register<ICommandHandler<OuterBridgeCommand>, OuterBridgeCommandHandler>(Lifestyle.Scoped);
        container.Register<ICommandHandler<InnerBridgeCommand>, InnerBridgeCommandHandler>(Lifestyle.Scoped);

        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureServiceBus:ConnectionString"] = "sample.servicebus.windows.net",
            })
            .Build();
        services.AddArkAzureFunctions();
        services.AddArkSolidProcessors(container);
        services.ConfigureArkMessagingFunctions(
            configuration,
            ArkGeneratedMessagingFunctions.Manifest,
            static messaging => messaging
                .UseTransport(static transport => transport.UseServiceBus())
                .UseDataBus(new InMemoryMessagingDataBus(SystemClock.Instance, Duration.FromHours(2)))
                .UseOutbox(static outbox => outbox.UseEnqueue()));
        services.AddArkAzureFunctionsSimpleInjectorBridge(container);
        await using var provider = services.BuildServiceProvider();
        _ = provider.GetServices<IHostedService>();
        var processor = provider.GetRequiredService<ICommandProcessor>();

        await processor.ExecuteAsync(new OuterBridgeCommand()).ConfigureAwait(false);
        await processor.ExecuteAsync(new OuterBridgeCommand()).ConfigureAwait(false);

        recorder.Observations.Should().HaveCount(2);
        recorder.Observations[0].OuterScopeId.Should().Be(recorder.Observations[0].InnerScopeId);
        recorder.Observations[1].OuterScopeId.Should().Be(recorder.Observations[1].InnerScopeId);
        recorder.Observations[0].OuterScopeId.Should().NotBe(recorder.Observations[1].OuterScopeId);
    }

    private sealed class EmptyContextProvider : IContextProvider<ClaimsPrincipal>
    {
        public ClaimsPrincipal Current => new(new ClaimsIdentity());
    }

    private sealed class BridgeScopeMarker
    {
        public Guid ScopeId { get; } = Guid.NewGuid();
    }

    private sealed class BridgeExecutionRecorder
    {
        public Guid InnerScopeId { get; set; }
        public List<BridgeScopeObservation> Observations { get; } = [];
    }

    private sealed record BridgeScopeObservation(Guid OuterScopeId, Guid InnerScopeId);

    private sealed record OuterBridgeCommand : ICommand<OuterBridgeCommand>;

    private sealed record InnerBridgeCommand : ICommand<InnerBridgeCommand>;

    private sealed class OuterBridgeCommandHandler : ICommandHandler<OuterBridgeCommand>
    {
        private readonly BridgeScopeMarker _marker;
        private readonly ICommandProcessor _processor;
        private readonly BridgeExecutionRecorder _recorder;

        public OuterBridgeCommandHandler(
            BridgeScopeMarker marker,
            ICommandProcessor processor,
            BridgeExecutionRecorder recorder)
        {
            _marker = marker;
            _processor = processor;
            _recorder = recorder;
        }

        public async Task ExecuteAsync(OuterBridgeCommand command, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            await _processor.ExecuteAsync(new InnerBridgeCommand(), ctk).ConfigureAwait(false);
            _recorder.Observations.Add(new BridgeScopeObservation(_marker.ScopeId, _recorder.InnerScopeId));
        }
    }

    private sealed class InnerBridgeCommandHandler : ICommandHandler<InnerBridgeCommand>
    {
        private readonly BridgeScopeMarker _marker;
        private readonly BridgeExecutionRecorder _recorder;

        public InnerBridgeCommandHandler(
            BridgeScopeMarker marker,
            BridgeExecutionRecorder recorder)
        {
            _marker = marker;
            _recorder = recorder;
        }

        public async Task ExecuteAsync(InnerBridgeCommand command, CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            _recorder.InnerScopeId = _marker.ScopeId;
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
