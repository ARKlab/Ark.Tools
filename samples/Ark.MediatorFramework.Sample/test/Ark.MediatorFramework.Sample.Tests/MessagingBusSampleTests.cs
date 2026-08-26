// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application.JsonContext;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;
using Ark.Tools.Solid.SimpleInjector;

using AwesomeAssertions;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies the sample's generated restricted bus composition.</summary>
[TestClass]
public sealed class MessagingBusSampleTests
{
    [TestMethod]
    public async Task SendRoutesBookPrintMessageToSampleParticipant()
    {
        var network = SampleMessagingNetwork.CreateOptions();
        var transport = new InMemoryMessagingTransport();
        var dataBus = new InMemoryMessagingDataBus();
        var codec = new JsonMessagingCodec(new JsonSerializerOptions
        {
            TypeInfoResolver = ApplicationJsonSerializerContext.Default
        });
        using var bus = new MessagingBus(
            transport,
            network,
            SampleMessagingNetwork.Registry,
            new MessagingCodecRegistry([codec]),
            SampleMessagingParticipant.CreatePayloadSender(dataBus, network),
            SampleMessagingParticipant.Identity);

        await bus.Send(new ProcessBookPrintProcessRequest { Id = Guid.NewGuid() }).ConfigureAwait(false);

        await foreach (var delivery in transport
            .ReceiveAsync(SampleMessagingParticipant.Identity, CancellationToken.None)
            .ConfigureAwait(false))
        {
            delivery.Headers[MessagingHeaders.MessageType]
                .Should().Be("ark_mediator_framework_sample_application_messages_process_book_print_process_request");
            delivery.Headers[MessagingHeaders.SenderIdentity]
                .Should().Be(SampleMessagingParticipant.Identity);
            codec.Deserialize<ProcessBookPrintProcessRequest>(delivery.Payload).Id.Should().NotBe(Guid.Empty);
            await delivery.CompleteAsync(default).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException("The sample bus did not produce a delivery.");
    }

    [TestMethod]
    public async Task InMemoryPumpRunsBookSecondLevelHandlerAtRetryBoundary()
    {
        var network = SampleMessagingNetwork.CreateOptions();
        var transport = new InMemoryMessagingTransport();
        var dataBus = new InMemoryMessagingDataBus();
        var codec = new JsonMessagingCodec(new JsonSerializerOptions
        {
            TypeInfoResolver = ApplicationJsonSerializerContext.Default
        });
        var retryPolicy = new SampleMessagingRetryPolicy();
        transport.ConfigureRetry(SampleMessagingParticipant.Identity, retryPolicy);
        await using var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        var state = new DispatchState();
        container.RegisterInstance(state);
        container.RegisterSingleton<ICommandProcessor, SimpleInjectorCommandProcessor>();
        container.Register<ICommandHandler<ProcessBookPrintProcessRequest>, FailingBookCommandHandler>(Lifestyle.Scoped);
        container.Register<ICommandHandler<IFailed<ProcessBookPrintProcessRequest>>, RecordingBookFailureHandler>(Lifestyle.Scoped);
        var dispatcher = new MessagingDispatcher(
            container,
            new MessagingHeaderProcessor(
                new MessagingCodecRegistry([codec]),
                network.NetworkIdentity),
            new MessagingPayloadReceiver(dataBus, network),
            retryPolicy,
            SampleMessagingParticipant.DispatchAsync,
            SampleMessagingParticipant.DispatchFailedAsync,
            lockRenewalInterval: TimeSpan.FromSeconds(1));
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var pump = new MessagingReceivePump(
            transport,
            SampleMessagingParticipant.Identity,
            async (delivery, ctk) =>
            {
                await dispatcher.OnDeliveryAsync(delivery, ctk).ConfigureAwait(false);
                if (state._failureExecutionCount == 1)
                    settled.TrySetResult();
            });
        using var bus = new MessagingBus(
            transport,
            network,
            SampleMessagingNetwork.Registry,
            new MessagingCodecRegistry([codec]),
            SampleMessagingParticipant.CreatePayloadSender(dataBus, network),
            SampleMessagingParticipant.Identity);

        await pump.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await bus.Send(new ProcessBookPrintProcessRequest { Id = Guid.NewGuid() }).ConfigureAwait(false);
        await settled.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        await pump.StopAsync().ConfigureAwait(false);

        state._normalExecutionCount.Should().Be(2);
        state._failureExecutionCount.Should().Be(1);
        transport.GetDeadLetters(SampleMessagingParticipant.Identity).Should().BeEmpty();
    }

    private sealed class DispatchState
    {
        private int _normalExecutions;
        private int _failureExecutions;

        internal int _normalExecutionCount => Volatile.Read(ref _normalExecutions);

        internal int _failureExecutionCount => Volatile.Read(ref _failureExecutions);

        internal void _recordNormal()
        {
            Interlocked.Increment(ref _normalExecutions);
        }

        internal void _recordFailure()
        {
            Interlocked.Increment(ref _failureExecutions);
        }
    }

    private sealed class FailingBookCommandHandler : ICommandHandler<ProcessBookPrintProcessRequest>
    {
        private readonly DispatchState _state;

        public FailingBookCommandHandler(DispatchState state)
        {
            _state = state;
        }

        public async Task ExecuteAsync(
            ProcessBookPrintProcessRequest command,
            CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            ctk.ThrowIfCancellationRequested();
            _state._recordNormal();
            await Task.CompletedTask.ConfigureAwait(false);
            throw new InvalidOperationException("Synthetic Book handler failure.");
        }
    }

    private sealed class RecordingBookFailureHandler :
        ICommandHandler<IFailed<ProcessBookPrintProcessRequest>>
    {
        private readonly DispatchState _state;

        public RecordingBookFailureHandler(DispatchState state)
        {
            _state = state;
        }

        public async Task ExecuteAsync(
            IFailed<ProcessBookPrintProcessRequest> command,
            CancellationToken ctk = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            command.DeliveryCount.Should().Be(2);
            command.Exceptions.Should().ContainSingle();
            ctk.ThrowIfCancellationRequested();
            _state._recordFailure();
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
