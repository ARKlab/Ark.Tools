// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

extern alias AuditFunctions;

using Ark.MediatorFramework.Sample.Application.JsonContext;
using Ark.MediatorFramework.Sample.AzureFunctions;
using Ark.MediatorFramework.Sample.WebInterface;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;
using Ark.Tools.Solid.SimpleInjector;

using AwesomeAssertions;

using Azure.Storage.Queues;

using Rebus.Transport.InMem;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies the sample's generated restricted bus composition.</summary>
[TestClass]
public sealed class MessagingBusSampleTests
{
    [TestMethod]
    public void WebInterfaceCompositionIsPublisherOnly()
    {
        using var container = SampleComposition.BuildContainer(new InMemNetwork(), useSqlStore: false);

        container.GetRegistration<ICommandHandler<BookPrintCompleted>>(throwOnFailure: false)
            .Should().BeNull();
    }

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
                .Should().Be("books_process_book_print_process");
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
        container.Register<ICommandHandler<MessagingFailed<ProcessBookPrintProcessRequest>>, RecordingBookFailureHandler>(Lifestyle.Scoped);
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

    [TestMethod]
    public async Task InMemoryPublishReachesDistinctBookPrintSubscribers()
    {
        var network = SampleMessagingNetwork.CreateOptions();
        var transport = new InMemoryMessagingTransport();
        var dataBus = new InMemoryMessagingDataBus();
        var codec = new JsonMessagingCodec(new JsonSerializerOptions
        {
            TypeInfoResolver = ApplicationJsonSerializerContext.Default
        });
        var retryPolicy = new SampleMessagingRetryPolicy();
        var maximumDeliveryCount = retryPolicy.MaximumDeliveryCount * 2;
        var topic = SampleMessagingNetwork.Registry.GetDestination<BookPrintCompleted>();
        await transport.EnsureQueueAsync(
                SampleMessagingNotificationParticipant.Identity,
                maximumDeliveryCount,
                SampleMessagingNotificationParticipant.Identity,
                default)
            .ConfigureAwait(false);
        await transport.EnsureQueueAsync(
                SampleMessagingAuditParticipant.Identity,
                maximumDeliveryCount,
                SampleMessagingAuditParticipant.Identity,
                default)
            .ConfigureAwait(false);
        await transport.EnsureTopicAsync(
                topic,
                SampleMessagingPublisherParticipant.Identity,
                default)
            .ConfigureAwait(false);
        await transport.EnsureSubscriptionAsync(
                new MessagingSubscriptionResource(
                    topic,
                    SampleMessagingNotificationParticipant.Identity,
                    SampleMessagingNotificationParticipant.Identity,
                    maximumDeliveryCount,
                    SampleMessagingNotificationParticipant.Identity),
                default)
            .ConfigureAwait(false);
        await transport.EnsureSubscriptionAsync(
                new MessagingSubscriptionResource(
                    topic,
                    SampleMessagingAuditParticipant.Identity,
                    SampleMessagingAuditParticipant.Identity,
                    maximumDeliveryCount,
                    SampleMessagingAuditParticipant.Identity),
                default)
            .ConfigureAwait(false);

        var notifications = new SubscriberState();
        var audits = new SubscriberState();
        #pragma warning disable MA0004 // Test-owned resources are disposed after the bounded run.
        await using var notificationSink = new RecordingNotificationSink(notifications);
        await using var auditSink = new RecordingAuditSink(audits);
        await using var notificationContainer = AzureFunctionsNativeComposition.BuildContainer(
            bookPrintNotificationSink: notificationSink);
        await using var auditContainer =
            AuditFunctions::Ark.MediatorFramework.Sample.AuditFunctions.AuditFunctionsComposition.BuildContainer(
            bookPrintAuditSink: auditSink);
        notificationContainer.RegisterInstance<IContextProvider<ClaimsPrincipal>>(new EmptyContextProvider());
        auditContainer.RegisterInstance<IContextProvider<ClaimsPrincipal>>(new EmptyContextProvider());
        var notificationDispatcher = _createDispatcher(
            notificationContainer,
            dataBus,
            network,
            codec,
            retryPolicy,
            SampleMessagingNotificationParticipant.DispatchAsync,
            SampleMessagingNotificationParticipant.DispatchFailedAsync);
        var auditDispatcher = _createDispatcher(
            auditContainer,
            dataBus,
            network,
            codec,
            retryPolicy,
            SampleMessagingAuditParticipant.DispatchAsync,
            SampleMessagingAuditParticipant.DispatchFailedAsync);
        await using var notificationPump = new MessagingReceivePump(
            transport,
            SampleMessagingNotificationParticipant.Identity,
            notificationDispatcher.OnDeliveryAsync);
        await using var auditPump = new MessagingReceivePump(
            transport,
            SampleMessagingAuditParticipant.Identity,
            auditDispatcher.OnDeliveryAsync);
        #pragma warning restore MA0004
        using var bus = new MessagingBus(
            transport,
            network,
            SampleMessagingNetwork.Registry,
            new MessagingCodecRegistry([codec]),
            SampleMessagingPublisherParticipant.CreatePayloadSender(dataBus, network),
            SampleMessagingPublisherParticipant.Identity);

        await notificationPump.StartAsync(default).ConfigureAwait(false);
        await auditPump.StartAsync(default).ConfigureAwait(false);
        await bus.Publish(new BookPrintCompleted { BookId = Guid.Parse("00000000-0000-0000-0000-000000000042") })
            .ConfigureAwait(false);
        await Task.WhenAll(
                notifications._completed.Task.WaitAsync(TimeSpan.FromSeconds(5)),
                audits._completed.Task.WaitAsync(TimeSpan.FromSeconds(5)))
            .ConfigureAwait(false);
        await notificationPump.StopAsync().ConfigureAwait(false);
        await auditPump.StopAsync().ConfigureAwait(false);

        notifications._bookIds.Should().ContainSingle();
        audits._bookIds.Should().ContainSingle();
        notifications._bookIds[0].Should().Be(audits._bookIds[0]);
    }

    [TestMethod]
    [TestCategory("integration")]
    public async Task StorageQueueRoutesScheduledBookMessageAndMovesPoison()
    {
        const string connectionString = "UseDevelopmentStorage=true";
        var options = new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.None
        };
        var service = new QueueServiceClient(connectionString, options);
        var queue = service.GetQueueClient(SampleMessagingParticipant.Identity);
        var poison = service.GetQueueClient(SampleMessagingParticipant.Identity + "-poison");
        await queue.DeleteIfExistsAsync(CancellationToken.None).ConfigureAwait(false);
        await poison.DeleteIfExistsAsync(CancellationToken.None).ConfigureAwait(false);
        var transport = new StorageQueueMessagingTransport(
            service,
            receiveVisibilityTimeout: TimeSpan.FromSeconds(30),
            retryDelay: new SampleMessagingRetryPolicy().RetryDelay);
        await transport.EnsureQueueAsync(
                SampleMessagingParticipant.Identity,
                4,
                SampleMessagingParticipant.Identity,
                default)
            .ConfigureAwait(false);
        var network = new MessagingNetworkOptions(
            typeof(SampleMessagingNetwork),
            new MessagingNetworkAttribute
            {
                Members =
                [
                    typeof(SampleMessagingPublisherParticipant),
                    typeof(SampleMessagingParticipant),
                    typeof(SampleMessagingNotificationParticipant),
                    typeof(SampleMessagingAuditParticipant),
                ],
                Requires = MessagingCapabilities.SendReceive | MessagingCapabilities.ScheduledSend,
                MaximumSchedulingDelaySeconds = 3600,
            });
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
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            var stopwatch = Stopwatch.StartNew();
            await bus.Defer(
                new ProcessBookPrintProcessRequest { Id = Guid.NewGuid() },
                TimeSpan.FromSeconds(2),
                cancellationToken: timeout.Token).ConfigureAwait(false);
#pragma warning disable MA0004 // The test disposes the enumerator at the end of the method.
            await using var enumerator = transport
                .ReceiveAsync(SampleMessagingParticipant.Identity, timeout.Token)
                .GetAsyncEnumerator(timeout.Token);
#pragma warning restore MA0004
            (await enumerator.MoveNextAsync().ConfigureAwait(false)).Should().BeTrue();
            stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(1));
            codec.Deserialize<ProcessBookPrintProcessRequest>(enumerator.Current.Payload)
                .Id.Should().NotBe(Guid.Empty);

            await enumerator.Current.DeadLetterAsync(
                "sample-failure",
                "Book sample poison proof",
                timeout.Token).ConfigureAwait(false);
            var poisonMessage = await poison.ReceiveMessageAsync(cancellationToken: timeout.Token)
                .ConfigureAwait(false);
            var envelope = StorageQueueEnvelopeCodec.Decode(poisonMessage.Value.Body);
            envelope.Headers[StorageQueuePoisonHeaders.Reason].Should().Be("sample-failure");
            codec.Deserialize<ProcessBookPrintProcessRequest>(envelope.Payload)
                .Id.Should().NotBe(Guid.Empty);
        }
        finally
        {
            await queue.DeleteIfExistsAsync(CancellationToken.None).ConfigureAwait(false);
            await poison.DeleteIfExistsAsync(CancellationToken.None).ConfigureAwait(false);
        }
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

    private static MessagingDispatcher _createDispatcher(
        Container container,
        InMemoryMessagingDataBus dataBus,
        MessagingNetworkOptions network,
        JsonMessagingCodec codec,
        IMessagingRetryPolicy retryPolicy,
        Func<string, IMessagingPayloadReader, ICommandProcessor, CancellationToken, Task> dispatch,
        Func<string, IMessagingPayloadReader, int, MessagingExceptionInfo, ICommandProcessor, CancellationToken, Task> dispatchFailed)
    {
        return new MessagingDispatcher(
            container,
            new MessagingHeaderProcessor(
                new MessagingCodecRegistry([codec]),
                network.NetworkIdentity),
            new MessagingPayloadReceiver(dataBus, network),
            retryPolicy,
            dispatch,
            dispatchFailed);
    }

    private sealed class EmptyContextProvider : IContextProvider<ClaimsPrincipal>
    {
        public ClaimsPrincipal Current => new(new ClaimsIdentity());
    }

    private sealed class SubscriberState
    {
        internal List<Guid> _bookIds { get; } = [];

        internal TaskCompletionSource _completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void _record(Guid bookId)
        {
            lock (_bookIds)
            {
                _bookIds.Add(bookId);
                _completed.TrySetResult();
            }
        }
    }

    private sealed class RecordingNotificationSink : IBookPrintNotificationSink, IAsyncDisposable
    {
        private readonly SubscriberState _state;

        public RecordingNotificationSink(SubscriberState state)
        {
            _state = state;
        }

        public async Task RecordAsync(Guid bookId, CancellationToken ctk = default)
        {
            ctk.ThrowIfCancellationRequested();
            _state._record(bookId);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    private sealed class RecordingAuditSink : IBookPrintAuditSink, IAsyncDisposable
    {
        private readonly SubscriberState _state;

        public RecordingAuditSink(SubscriberState state)
        {
            _state = state;
        }

        public async Task RecordAsync(Guid bookId, CancellationToken ctk = default)
        {
            ctk.ThrowIfCancellationRequested();
            _state._record(bookId);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await Task.CompletedTask.ConfigureAwait(false);
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
        ICommandHandler<MessagingFailed<ProcessBookPrintProcessRequest>>
    {
        private readonly DispatchState _state;

        public RecordingBookFailureHandler(DispatchState state)
        {
            _state = state;
        }

        public async Task ExecuteAsync(
            MessagingFailed<ProcessBookPrintProcessRequest> command,
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
