// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

extern alias Audit;
extern alias Notifications;
extern alias Processor;

using Ark.MediatorFramework.Sample.Core.Functions.Hosting;
using Ark.Tools.MediatorFramework;
using Ark.Tools.MediatorFramework.AzureFunctions;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NodaTime;

using SimpleInjector;

using System.Reflection;

using AuditFunctions = Audit::Ark.Tools.MediatorFramework.AzureFunctions.Generated.ArkGeneratedMessagingFunctions;
using NotificationsFunctions = Notifications::Ark.Tools.MediatorFramework.AzureFunctions.Generated.ArkGeneratedMessagingFunctions;
using ProcessorFunctions = Processor::Ark.Tools.MediatorFramework.AzureFunctions.Generated.ArkGeneratedMessagingFunctions;

namespace Ark.MediatorFramework.Sample.Core.Functions.Tests;

/// <summary>Composes each Functions app as its <c>Program.cs</c> does.</summary>
[TestClass]
public sealed class FunctionsCompositionTests
{
    // Composition only: the namespace is not contacted because no hosted service is started.
    private static readonly IConfiguration _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureServiceBus:ConnectionString"] = "sample.servicebus.windows.net",
        })
        .Build();

    /// <summary>The Api app hosts a producer: it sends through the outbox and receives nothing.</summary>
    [TestMethod]
    public async Task ApiAppIsProducerOnly()
    {
        await using var container = FunctionsHosting.CreateContainer(_inMemoryOptions());
        var services = new ServiceCollection();
        services.AddLogging();
        FunctionsHosting.AddApiProducer(
            services,
            container,
            static transport => transport.UseInMemory(),
            static dataBus => dataBus.UseInMemory(lifetime: Duration.FromHours(2)));
        await using var provider = services.BuildServiceProvider();

        services.Should().NotContain(static service => service.ServiceType == typeof(MessagingTriggeredHostMarker));
        provider.GetServices<IHostedService>().OfType<MessagingProcessorHost>().Should().BeEmpty();
        provider.GetRequiredService<IBus>().Should().NotBeNull();
    }

    /// <summary>Each trigger app is generated for exactly one participant.</summary>
    [TestMethod]
    public void EachTriggerAppDeclaresOneParticipant()
    {
        _participant(typeof(ProcessorFunctions)).Should().Be<SampleMessagingParticipant>();
        _participant(typeof(NotificationsFunctions)).Should().Be<SampleMessagingNotificationParticipant>();
        _participant(typeof(AuditFunctions)).Should().Be<SampleMessagingAuditParticipant>();
    }

    /// <summary>Each trigger app finds every generated handler and never polls the outbox.</summary>
    /// <param name="app">The trigger app.</param>
    [TestMethod]
    [DataRow("Processor")]
    [DataRow("Notifications")]
    [DataRow("Audit")]
    public async Task EachTriggerAppComposesItsParticipant(string app)
    {
        await using var container = FunctionsHosting.CreateContainer(_inMemoryOptions());
        var manifest = app switch
        {
            "Processor" => ProcessorFunctions.Manifest,
            "Notifications" => _withNotificationSubscriber(container),
            "Audit" => _withAuditSubscriber(container),
            _ => throw new ArgumentOutOfRangeException(nameof(app), app, "Unknown trigger app."),
        };
        var services = new ServiceCollection();
        services.AddLogging();

        FunctionsHosting.AddMessagingTrigger(services, _configuration, manifest, container, static dataBus => dataBus.Use(new InMemoryMessagingDataBus(SystemClock.Instance, Duration.FromHours(2))));
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<MessagingTriggeredHostMarker>().Should().NotBeNull();
        var hosted = provider.GetServices<IHostedService>().ToArray();
        hosted.OfType<MessagingProcessorHost>().Should().BeEmpty();
        hosted.OfType<MessagingOutboxProcessor>().Should().BeEmpty();
    }

    /// <summary>Bridges the Processor app's container into Microsoft DI for scoped processor dispatch.</summary>
    [TestMethod]
    public async Task ProcessorAppBridgesScopedCommandProcessingIntoMicrosoftDependencyInjection()
    {
        await using var container = FunctionsHosting.CreateContainer(_inMemoryOptions());
        var recorder = new BridgeExecutionRecorder();
        container.Register<BridgeScopeMarker>(Lifestyle.Scoped);
        container.RegisterInstance(recorder);
        container.Register<ICommandHandler<OuterBridgeCommand>, OuterBridgeCommandHandler>(Lifestyle.Scoped);
        container.Register<ICommandHandler<InnerBridgeCommand>, InnerBridgeCommandHandler>(Lifestyle.Scoped);

        var services = new ServiceCollection();
        services.AddLogging();
        FunctionsHosting.AddMessagingTrigger(
            services, _configuration, ProcessorFunctions.Manifest, container, static dataBus => dataBus.Use(new InMemoryMessagingDataBus(SystemClock.Instance, Duration.FromHours(2))));
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

    private static ApplicationOptions _inMemoryOptions()
    {
        return new ApplicationOptions
        {
            DataContextFactory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory()),
        };
    }

    private static MessagingFunctionsManifest _withNotificationSubscriber(Container container)
    {
        ApplicationComposition.RegisterNotificationSubscriber(container, new NoOpBookPrintNotificationSink());
        return NotificationsFunctions.Manifest;
    }

    private static MessagingFunctionsManifest _withAuditSubscriber(Container container)
    {
        ApplicationComposition.RegisterAuditSubscriber(container, new NoOpBookPrintAuditSink());
        return AuditFunctions.Manifest;
    }

    private static Type _participant(Type generatedFunctions)
    {
        return generatedFunctions.Assembly.GetCustomAttribute<MessagingFunctionsHostAttribute>()!.Participant;
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
