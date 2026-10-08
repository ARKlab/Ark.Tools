// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.AzureFunctions;

using Ark.Tools.MediatorFramework.AzureFunctions;
using Ark.Tools.MediatorFramework.AzureFunctions.Generated;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;
using Ark.Tools.MediatorFramework.AzureFunctions.SimpleInjector;
using Ark.Tools.Solid.SimpleInjector;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using NodaTime;
using System.Buffers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies the native Functions composition registers no Rebus bus.</summary>
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
