// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Google.Protobuf.WellKnownTypes;

using MessagePack;
using MessagePack.Resolvers;

using Microsoft.Extensions.DependencyInjection;

using NodaTime;
using NodaTime.Testing;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the transport-neutral messaging runtime seams.</summary>
[TestClass]
public sealed partial class MessagingRuntimeTests
{
    [TestMethod]
    public void JsonCodecRoundTripsThroughBufferWriterAndSequence()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = MessagingTestJsonContext.Default
        };
        var codec = new JsonMessagingCodec(options);
        var writer = new ArrayBufferWriter<byte>();

        codec.Serialize(new MessagingRuntimeContract { Name = "Ada", Data = [1, 2, 3] }, writer);
        var result = codec.Deserialize<MessagingRuntimeContract>(
            new ReadOnlySequence<byte>(writer.WrittenMemory));

        result.Name.Should().Be("Ada");
        result.Data.Should().Equal(1, 2, 3);
    }

    [TestMethod]
    public void HeaderProcessorResolvesCodecAndRejectsForeignNetwork()
    {
        var codec = new JsonMessagingCodec(new JsonSerializerOptions
        {
            TypeInfoResolver = MessagingTestJsonContext.Default
        });
        var processor = new MessagingHeaderProcessor(
            new MessagingCodecRegistry([codec]),
            "books-network");

        var classified = processor.Classify(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingHeaders.MessageType] = "books_print_book",
            [MessagingHeaders.ContentType] = codec.ContentType,
            [MessagingHeaders.Network] = "books-network"
        });

        classified.Codec.Should().BeSameAs(codec);
        classified.LogicalName.Should().Be("books_print_book");

        var action = () => processor.Classify(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingHeaders.MessageType] = "books_print_book",
            [MessagingHeaders.ContentType] = codec.ContentType,
            [MessagingHeaders.Network] = "other-network"
        });

        action.Should().Throw<MessagingFailFastException>().Which.Reason
            .Should().Be(MessagingFailFastReason.ForeignNetwork);
    }

    [TestMethod]
    public void CountingWriterFailsBeforeAdvancingInnerWriter()
    {
        var inner = new ArrayBufferWriter<byte>();
        var writer = new CountingBufferWriter(inner, 2);
        writer.GetSpan(2)[0] = 1;
        writer.Advance(2);

        var action = () => writer.Advance(1);

        action.Should().Throw<MessagingFailFastException>().Which.Reason
            .Should().Be(MessagingFailFastReason.OversizedPayload);
        writer.BytesWritten.Should().Be(2);
        inner.WrittenCount.Should().Be(2);
    }

    [TestMethod]
    public void StartupValidationRejectsMissingJsonMetadata()
    {
        var action = static () => MessagingJsonStartupValidation.ValidateContract<MessagingRuntimeContract>(
            new JsonSerializerOptions());

        action.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("JsonSerializerContext");
    }

    [TestMethod]
    public void CodecRegistryRejectsUnknownContentType()
    {
        var codec = new JsonMessagingCodec(new JsonSerializerOptions
        {
            TypeInfoResolver = MessagingTestJsonContext.Default
        });
        var registry = new MessagingCodecRegistry([codec]);

        var action = () => registry.GetByContentType("application/unknown");

        action.Should().Throw<MessagingFailFastException>().Which.Reason
            .Should().Be(MessagingFailFastReason.UnknownContentType);
    }

    [TestMethod]
    public void CodecRegistryRejectsUnknownProtocol()
    {
        var codec = new JsonMessagingCodec(new JsonSerializerOptions
        {
            TypeInfoResolver = MessagingTestJsonContext.Default
        });
        var registry = new MessagingCodecRegistry([codec]);

        var action = () => registry.GetByProtocol((SerializationProtocol)999);

        action.Should().Throw<MessagingFailFastException>().Which.Reason
            .Should().Be(MessagingFailFastReason.UnknownProtocol);
    }

    [TestMethod]
    public void MessagePackCodecRoundTripsWithUntrustedDataOptions()
    {
        var codec = new MessagePackMessagingCodec(StandardResolver.Instance);
        var writer = new ArrayBufferWriter<byte>();

        codec.Serialize(new MessagePackRuntimeContract { Name = "Ada" }, writer);
        var result = codec.Deserialize<MessagePackRuntimeContract>(
            new ReadOnlySequence<byte>(writer.WrittenMemory));

        result.Name.Should().Be("Ada");
    }

    [TestMethod]
    public void MultipleCodecsRoundTripPayloadsSelectedByContentType()
    {
        var messagePack = new MessagePackMessagingCodec(StandardResolver.Instance);
        var protobuf = new ProtobufMessagingCodec();
        var registry = new MessagingCodecRegistry([messagePack, protobuf]);

        try
        {
            ProtobufContractRegistry<Empty>.Parse = static payload => Empty.Parser.ParseFrom(payload);
            var messagePackWriter = new ArrayBufferWriter<byte>();
            messagePack.Serialize(new MessagePackRuntimeContract { Name = "Ada" }, messagePackWriter);
            var selectedMessagePack = registry.GetByContentType(messagePack.ContentType);
            selectedMessagePack.Should().BeSameAs(messagePack);
            selectedMessagePack.Deserialize<MessagePackRuntimeContract>(
                new ReadOnlySequence<byte>(messagePackWriter.WrittenMemory)).Name.Should().Be("Ada");

            var protobufWriter = new ArrayBufferWriter<byte>();
            protobuf.Serialize(new Empty(), protobufWriter);
            var selectedProtobuf = registry.GetByContentType(protobuf.ContentType);
            selectedProtobuf.Should().BeSameAs(protobuf);
            selectedProtobuf.Deserialize<Empty>(
                new ReadOnlySequence<byte>(protobufWriter.WrittenMemory)).Should().NotBeNull();
        }
        finally
        {
            ProtobufContractRegistry<Empty>.Parse = null;
        }
    }

    [TestMethod]
    public void MalformedPayloadsFailFast()
    {
        var messagePack = new MessagePackMessagingCodec(StandardResolver.Instance);
        var protobuf = new ProtobufMessagingCodec();

        var messagePackAction = () => messagePack.Deserialize<MessagePackRuntimeContract>(
            new ReadOnlySequence<byte>(new byte[] { MessagePackCode.Map16, 0, 255 }));
        messagePackAction.Should().Throw<MessagePackSerializationException>();

        try
        {
            ProtobufContractRegistry<Empty>.Parse = static payload => Empty.Parser.ParseFrom(payload);
            var protobufAction = () => protobuf.Deserialize<Empty>(
                new ReadOnlySequence<byte>(new byte[] { 255 }));

            protobufAction.Should().Throw<Google.Protobuf.InvalidProtocolBufferException>();
        }
        finally
        {
            ProtobufContractRegistry<Empty>.Parse = null;
        }
    }

    [TestMethod]
    public void ProtobufCodecRoundTripsThroughRegisteredParser()
    {
        try
        {
            ProtobufContractRegistry<Empty>.Parse = static payload => Empty.Parser.ParseFrom(payload);
            var codec = new ProtobufMessagingCodec();
            var writer = new ArrayBufferWriter<byte>();

            codec.Serialize(new Empty(), writer);
            var result = codec.Deserialize<Empty>(new ReadOnlySequence<byte>(writer.WrittenMemory));

            result.Should().NotBeNull();
        }
        finally
        {
            ProtobufContractRegistry<Empty>.Parse = null;
        }
    }

    [TestMethod]
    public void StartupValidationRejectsUninstalledDeclaredSerializer()
    {
        var codec = new JsonMessagingCodec(new JsonSerializerOptions
        {
            TypeInfoResolver = MessagingTestJsonContext.Default
        });

        var action = () => MessagingJsonStartupValidation.ValidateDeclaredSerializers(
            new MessagingCodecRegistry([codec]),
            [SerializationProtocol.Json, SerializationProtocol.MessagePack],
            "books");

        action.Should().Throw<MessagingFailFastException>()
            .Which.Reason.Should().Be(MessagingFailFastReason.UnknownProtocol);
    }

    [TestMethod]
    public void SettlementUsesNativeDeliveryCountAndSecondLevelBoundary()
    {
        var policy = new TestRetryPolicy(3, secondLevelRetriesEnabled: true);

        MessagingSettlement.Decide(1, policy, MessagingExceptionClassification.Other, false)
            .Should().Be(MessagingSettlementDecision.Abandon);
        MessagingSettlement.Decide(3, policy, MessagingExceptionClassification.Other, false)
            .Should().Be(MessagingSettlementDecision.RunSecondLevel);
        MessagingSettlement.Decide(4, policy, MessagingExceptionClassification.Other, false)
            .Should().Be(MessagingSettlementDecision.Abandon);
        MessagingSettlement.Decide(3, policy, MessagingExceptionClassification.FailFast, false)
            .Should().Be(MessagingSettlementDecision.DeadLetter);
        MessagingSettlement.Decide(3, policy, MessagingExceptionClassification.Other, true)
            .Should().Be(MessagingSettlementDecision.Abandon);
    }

    [TestMethod]
    public void RetryPolicyValidationRejectsInvalidSecondLevelCount()
    {
        var action = static () => MessagingRetryPolicyValidation.Validate(
            new TestRetryPolicy(1, secondLevelRetriesEnabled: true));

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void ExceptionInfoIsBoundedAndRetainsInnerExceptions()
    {
        var exception = new InvalidOperationException(
            new string('m', 300),
            new ArgumentException(new string('i', 300)));

        var info = MessagingExceptionInfo.From(exception);

        info.ExceptionType.Should().Be(typeof(InvalidOperationException).FullName);
        info.Message.Length.Should().Be(256);
        info.Inner.Should().NotBeNull();
        info.Inner!.Message.Length.Should().Be(256);
    }

    [TestMethod]
    public void CodecRegistrationInstallsAllDeclaredProtocols()
    {
        using var services = new ServiceCollection()
            ._addArkMessaging()
            ._addMessagePackAndProtobufMessagingCodecs()
            .BuildServiceProvider();

        var registry = services.GetRequiredService<IMessagingCodecRegistry>();

        registry.IsInstalled(SerializationProtocol.Json).Should().BeTrue();
        registry.IsInstalled(SerializationProtocol.MessagePack).Should().BeTrue();
        registry.IsInstalled(SerializationProtocol.Protobuf).Should().BeTrue();
    }

    [TestMethod]
    public async Task DispatcherCompletesSuccessfulDelivery()
    {
        await using var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.Register<ICommandProcessor, TestCommandProcessor>(Lifestyle.Scoped);
        var delivery = new TestLockedDelivery(1);
        var dispatcher = _createDispatcher(
            container,
            new TestRetryPolicy(3, secondLevelRetriesEnabled: false),
            static async (_, payload, _, token) =>
            {
                await payload.DeserializeAsync<DispatchCommand>(token).ConfigureAwait(false);
            });

        await dispatcher.OnDeliveryAsync(delivery, CancellationToken.None).ConfigureAwait(false);

        delivery._completed.Should().Be(1);
        delivery._abandoned.Should().Be(0);
        delivery._deadLetters.Should().BeEmpty();
    }

    [TestMethod]
    public async Task DispatcherDeadLettersMalformedPayloadWithoutSecondLevelDispatch()
    {
        await using var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.Register<ICommandProcessor, TestCommandProcessor>(Lifestyle.Scoped);
        var delivery = new TestLockedDelivery(
            2,
            new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("{")));
        var secondLevelDispatched = false;
        var dispatcher = _createDispatcher(
            container,
            new TestRetryPolicy(2, secondLevelRetriesEnabled: true),
            static async (_, payload, _, token) =>
            {
                await payload.DeserializeAsync<DispatchCommand>(token).ConfigureAwait(false);
            },
            (_, _, _, _, _, _) =>
            {
                secondLevelDispatched = true;
                return Task.CompletedTask;
            });

        await dispatcher.OnDeliveryAsync(delivery, CancellationToken.None).ConfigureAwait(false);

        delivery._completed.Should().Be(0);
        delivery._abandoned.Should().Be(0);
        delivery._deadLetters.Should().ContainSingle();
        delivery._deadLetterReason.Should().Be(MessagingFailFastReason.MalformedPayload.ToString());
        secondLevelDispatched.Should().BeFalse();
    }

    [TestMethod]
    public async Task DispatcherRunsFailureHandlerOnceAtRetryBoundary()
    {
        await using var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.Register<ICommandProcessor, TestCommandProcessor>(Lifestyle.Scoped);
        container.Register<ICommandHandler<MessagingFailed<DispatchCommand>>, RecordingFailedHandler>(Lifestyle.Scoped);
        var failureHandled = new List<MessagingFailed<DispatchCommand>>();
        var delivery = new TestLockedDelivery(2);
        var dispatcher = _createDispatcher(
            container,
            new TestRetryPolicy(2, secondLevelRetriesEnabled: true),
            static (_, _, _, _) => throw new InvalidOperationException("handler failed"),
            async (_, payload, count, error, processor, token) =>
            {
                var message = await payload.DeserializeAsync<DispatchCommand>(token).ConfigureAwait(false);
                var failure = new MessagingFailed<DispatchCommand>(message, count, [error]);
                failureHandled.Add(failure);
                await processor.ExecuteAsync<MessagingFailed<DispatchCommand>>(failure, token).ConfigureAwait(false);
            });

        await dispatcher.OnDeliveryAsync(delivery, CancellationToken.None).ConfigureAwait(false);

        delivery._completed.Should().Be(1);
        delivery._abandoned.Should().Be(0);
        delivery._deadLetters.Should().BeEmpty();
        failureHandled.Should().ContainSingle();
        failureHandled[0].DeliveryCount.Should().Be(2);
        failureHandled[0].ErrorDescription.Should().Contain("handler failed");
        container.GetRegistration<ICommandHandler<MessagingFailed<DispatchCommand>>>().Should().NotBeNull();
    }

    [TestMethod]
    public async Task DispatcherDeadLettersWhenFailureHandlerIsMissing()
    {
        await using var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.Register<ICommandProcessor, TestCommandProcessor>(Lifestyle.Scoped);
        var delivery = new TestLockedDelivery(2);
        var dispatcher = _createDispatcher(
            container,
            new TestRetryPolicy(2, secondLevelRetriesEnabled: true),
            static (_, _, _, _) => throw new InvalidOperationException("handler failed"),
            static (_, _, _, _, _, _) => throw new ActivationException("missing handler"));

        await dispatcher.OnDeliveryAsync(delivery, CancellationToken.None).ConfigureAwait(false);

        delivery._completed.Should().Be(0);
        delivery._abandoned.Should().Be(0);
        delivery._deadLetters.Should().ContainSingle().Which.Should().Be("tests.dispatch");
        delivery._deadLetterReason.Should().Be(typeof(MessagingFailFastException).FullName);
    }

    [TestMethod]
    public async Task DispatcherAbandonsWhenHandlerExceedsMaximumDuration()
    {
        await using var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.Register<ICommandProcessor, TestCommandProcessor>(Lifestyle.Scoped);
        var delivery = new TestLockedDelivery(1);
        var dispatcher = _createDispatcher(
            container,
            new TestRetryPolicy(
                3,
                secondLevelRetriesEnabled: false,
                maximumHandlerDuration: TimeSpan.FromMilliseconds(30)),
            static async (_, _, _, _) =>
                await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None).ConfigureAwait(false),
            lockRenewalInterval: TimeSpan.FromMilliseconds(5));

        await dispatcher.OnDeliveryAsync(delivery, CancellationToken.None).ConfigureAwait(false);

        delivery._completed.Should().Be(0);
        delivery._abandoned.Should().Be(1);
        delivery._renewals.Should().BeGreaterThan(0).And.BeLessThan(15);
    }

    [TestMethod]
    public void DispatcherRequiresFailureBinderWhenSecondLevelIsEnabled()
    {
        using var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.Register<ICommandProcessor, TestCommandProcessor>(Lifestyle.Scoped);

        var act = () => _createDispatcher(
            container,
            new TestRetryPolicy(2, secondLevelRetriesEnabled: true),
            static async (_, payload, _, token) =>
            {
                await payload.DeserializeAsync<DispatchCommand>(token).ConfigureAwait(false);
            });

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("dispatchFailed");
    }

    [TestMethod]
    public async Task PipelineRunsStepsInDeclaredOrderAndProtectsReservedHeaders()
    {
        var order = new List<string>();
        var context = new MessagingOutgoingContext(
            new Dictionary<string, string>(StringComparer.Ordinal),
            "books");
        var cancellationTokens = new List<CancellationToken>();
        var resolvedCount = 0;
        var stepTypes = new[] { typeof(RecordingOutgoingStep), typeof(RecordingOutgoingStep) };
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        async Task InvokeAsync()
        {
            await MessagingPipelineInvoker.InvokeOutgoingAsync(
                stepTypes,
                _ =>
                {
                    resolvedCount++;
                    return new RecordingOutgoingStep(
                        resolvedCount % 2 == 1 ? "first" : "second",
                        order,
                        cancellationTokens);
                },
                context,
                () =>
                {
                    order.Add("terminal");
                    return Task.CompletedTask;
                },
                cancellationToken).ConfigureAwait(false);
        }

        await InvokeAsync().ConfigureAwait(false);
        await InvokeAsync().ConfigureAwait(false);

        order.Should().Equal(
            "first", "second", "terminal",
            "first", "second", "terminal");
        resolvedCount.Should().Be(4);
        cancellationTokens.Should().HaveCount(4);
        foreach (var token in cancellationTokens)
            token.Should().Be(cancellationToken);
        var action = () => context.Headers[MessagingHeaders.MessageType] = "spoofed";
        action.Should().Throw<InvalidOperationException>();
        var differentlyCasedAction = () => context.Headers["AMF1-message-type"] = "spoofed";
        differentlyCasedAction.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public async Task UserContextStepsRoundTripClaims()
    {
        ClaimsPrincipal? restored = null;
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var outgoing = new MessagingOutgoingContext(
            headers,
            "books");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Email, "ada@example.test"),
            new Claim(ClaimTypes.Role, "admin")
        ], "test"));
        await new UserContextOutgoingStep(() => principal)
            .ProcessAsync(outgoing, static () => Task.CompletedTask, CancellationToken.None).ConfigureAwait(false);

        var incoming = new MessagingIncomingContext(headers, default);
        await new UserContextIncomingStep(value => restored = value)
            .ProcessAsync(incoming, static () => Task.CompletedTask, CancellationToken.None).ConfigureAwait(false);

        restored!.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("42");
        headers.Should().NotContainKey("ark-user-email");
        restored.FindFirst(ClaimTypes.Email).Should().BeNull();
        restored.IsInRole("admin").Should().BeTrue();
    }

    [TestMethod]
    public async Task OpenTelemetryStepsPropagateAzureDiagnosticId()
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var outgoing = new MessagingOutgoingContext(headers, "books");
        string diagnosticId;
        ActivityTraceId producerTraceId;
        using (var producer = new Activity("producer"))
        {
            producer.SetIdFormat(ActivityIdFormat.W3C);
            producer.Start();
            producer.AddBaggage("tenant", "a,b=value");

            await new OpenTelemetryOutgoingStep()
                .ProcessAsync(outgoing, static () => Task.CompletedTask, CancellationToken.None)
                .ConfigureAwait(false);

            diagnosticId = producer.Id!;
            producerTraceId = producer.TraceId;
        }

        headers[MessagingHeaders.DiagnosticId].Should().Be(diagnosticId);
        headers.Should().NotContainKey("traceparent");
        headers.Should().NotContainKey("tracestate");
        headers["baggage"].Should().Be("tenant=a%2Cb%3Dvalue");
        headers["baggage"] += ",invalid=%ZZ";

        Activity? received = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == OpenTelemetryIncomingStep.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => received = activity
        };
        ActivitySource.AddActivityListener(listener);

        var incoming = new MessagingIncomingContext(headers, default);
        await new OpenTelemetryIncomingStep()
            .ProcessAsync(incoming, static () => Task.CompletedTask, CancellationToken.None)
            .ConfigureAwait(false);

        received.Should().NotBeNull();
        received!.ParentId.Should().Be(diagnosticId);
        received.TraceId.Should().Be(producerTraceId);
        received.Baggage.Should().Contain(static x => x.Key == "tenant" && x.Value == "a,b=value");
        received.Baggage.Should().NotContain(static x => x.Key == "invalid");
    }

    [TestMethod]
    public async Task OpenTelemetryProcessingMetricsStepRecordsQueueAndProcessingMetrics()
    {
        var measurements = new List<(string Name, double Value, string? MessageType, string? Outcome)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = static (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == OpenTelemetryProcessingMetricsStep.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            string? messageType = null;
            string? operationResult = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "messaging.message.type")
                    messageType = tag.Value?.ToString();
                else if (tag.Key == "messaging.process.result")
                    operationResult = tag.Value?.ToString();
            }

            measurements.Add((instrument.Name, value, messageType, operationResult));
        });
        listener.Start();

        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var sentTime = clock.GetCurrentInstant().ToDateTimeOffset().AddSeconds(-2);
        var successContext = new MessagingIncomingContext(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MessagingHeaders.MessageType] = "tests.Message",
                [MessagingHeaders.SentTime] = sentTime.ToString("O", CultureInfo.InvariantCulture)
            },
            deliveryCount: 2);
        var step = new OpenTelemetryProcessingMetricsStep(clock);
        await step.ProcessAsync(successContext, static () => Task.CompletedTask, CancellationToken.None)
            .ConfigureAwait(false);

        var failureContext = new MessagingIncomingContext(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MessagingHeaders.MessageType] = "tests.Message"
            },
            deliveryCount: 2);
        Func<Task> processFailure = () => step.ProcessAsync(
            failureContext,
            static () => throw new InvalidOperationException("handler failed"),
            CancellationToken.None);
        await processFailure.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(false);

        var producerHeaders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingHeaders.Network] = "tests-network",
            [MessagingHeaders.SenderIdentity] = "tests-sender",
            [MessagingHeaders.MessageType] = "tests.Message",
        };
        MessagingMetrics.RecordClientOperation(
            TimeSpan.FromSeconds(0.25), producerHeaders, "send", "tests-queue");
        MessagingMetrics.RecordClientOperation(
            TimeSpan.FromSeconds(0.25), producerHeaders, "publish", "tests-topic");
        MessagingMetrics.RecordClientOperation(
            TimeSpan.FromSeconds(0.25), producerHeaders, "defer", "tests-queue");

        measurements.Should().Contain(static x =>
            x.Name == MessagingMetrics.TimeInQueueName
            && x.MessageType == "tests.Message"
            && x.Value > 1.5);
        measurements.Should().Contain(static x =>
            x.Name == MessagingMetrics.ProcessDurationName
            && x.MessageType == "tests.Message"
            && x.Outcome == "complete");
        measurements.Should().Contain(static x =>
            x.Name == MessagingMetrics.ProcessDurationName
            && x.MessageType == "tests.Message"
            && x.Outcome == "error");
        measurements.Should().Contain(static x =>
            x.Name == MessagingMetrics.DeliveryAttemptsName
            && Math.Abs(x.Value - 2d) < 1e-9);
        measurements.Should().Contain(static x =>
            x.Name == MessagingMetrics.ClientOperationDurationName
            && Math.Abs(x.Value - 0.25d) < 1e-9);
    }

    private static MessagingDispatcher _createDispatcher(
        Container container,
        IMessagingRetryPolicy retryPolicy,
        Func<string, IMessagingPayloadReader, ICommandProcessor, CancellationToken, Task> dispatch,
        Func<
            string,
            IMessagingPayloadReader,
            int,
            MessagingExceptionInfo,
            ICommandProcessor,
            CancellationToken,
            Task>? dispatchFailed = null,
        TimeSpan? lockRenewalInterval = null)
    {
        var codec = new JsonMessagingCodec(new JsonSerializerOptions
        {
            TypeInfoResolver = MessagingTestJsonContext.Default
        });
        var registry = new MessagingCodecRegistry([codec]);
        var network = new MessagingNetworkOptions(
            typeof(MessagingRuntimeTests),
            new MessagingNetworkAttribute());
        var payloadReceiver = new MessagingPayloadReceiver(
            new InMemoryMessagingDataBus(),
            network);
        return new MessagingDispatcher(
            container,
            new MessagingHeaderProcessor(registry, "tests"),
            payloadReceiver,
            retryPolicy,
            dispatch,
            dispatchFailed,
            lockRenewalInterval: lockRenewalInterval ?? TimeSpan.FromHours(1));
    }

    private sealed class TestLockedDelivery : IMessagingLockedDelivery
    {
        internal TestLockedDelivery(
            int deliveryCount,
            ReadOnlySequence<byte>? payload = null)
        {
            DeliveryCount = deliveryCount;
            var codec = new JsonMessagingCodec(new JsonSerializerOptions
            {
                TypeInfoResolver = MessagingTestJsonContext.Default
            });
            var writer = new ArrayBufferWriter<byte>();
            codec.Serialize(new DispatchCommand { Value = "test" }, writer);
            Payload = payload ?? new ReadOnlySequence<byte>(writer.WrittenMemory);
            Headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MessagingHeaders.MessageType] = "tests.dispatch",
                [MessagingHeaders.ContentType] = codec.ContentType,
                [MessagingHeaders.Network] = "tests"
            };
        }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public ReadOnlySequence<byte> Payload { get; }

        public int DeliveryCount { get; }

        public string DeliveryId { get; } = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        public DateTimeOffset? LockedUntil => null;

        internal int _completed { get; private set; }

        internal int _abandoned { get; private set; }

        internal List<string> _deadLetters { get; } = [];

        internal string? _deadLetterReason { get; private set; }

        internal int _renewals { get; private set; }

        public Task RenewLockAsync(CancellationToken ctk)
        {
            ctk.ThrowIfCancellationRequested();
            _renewals++;
            return Task.CompletedTask;
        }

        public Task CompleteAsync(CancellationToken ctk)
        {
            ctk.ThrowIfCancellationRequested();
            _completed++;
            return Task.CompletedTask;
        }

        public Task AbandonAsync(CancellationToken ctk)
        {
            ctk.ThrowIfCancellationRequested();
            _abandoned++;
            return Task.CompletedTask;
        }

        public Task DeadLetterAsync(string reason, string description, CancellationToken ctk)
        {
            ctk.ThrowIfCancellationRequested();
            _deadLetterReason = reason;
            _deadLetters.Add(description);
            return Task.CompletedTask;
        }
    }

    private sealed class TestCommandProcessor : ICommandProcessor
    {
        [Obsolete("Test seam.", error: true)]
        public void Execute(ICommand command)
        {
            throw new NotSupportedException();
        }

        public Task ExecuteAsync(ICommand command, CancellationToken ctk = default)
        {
            throw new NotSupportedException();
        }

        public async Task ExecuteAsync<TCommand>(ICommand<TCommand> command, CancellationToken ctk = default)
            where TCommand : class, ICommand<TCommand>
        {
            ctk.ThrowIfCancellationRequested();
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    private sealed class RecordingFailedHandler : ICommandHandler<MessagingFailed<DispatchCommand>>
    {
        public async Task ExecuteAsync(MessagingFailed<DispatchCommand> command, CancellationToken ctk = default)
        {
            ctk.ThrowIfCancellationRequested();
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    private sealed class RecordingOutgoingStep : IMessagingOutgoingStep
    {
        private readonly string _name;
        private readonly IList<string> _order;
        private readonly IList<CancellationToken> _cancellationTokens;

        public RecordingOutgoingStep(
            string name,
            IList<string> order,
            IList<CancellationToken> cancellationTokens)
        {
            _name = name;
            _order = order;
            _cancellationTokens = cancellationTokens;
        }

        public async Task ProcessAsync(
            MessagingOutgoingContext context,
            Func<Task> next,
            CancellationToken cancellationToken)
        {
            _order.Add(_name);
            _cancellationTokens.Add(cancellationToken);
            await next().ConfigureAwait(false);
        }
    }

    private sealed class TestRetryPolicy : IMessagingRetryPolicy
    {
        public TestRetryPolicy(
            int maximumDeliveryCount,
            bool secondLevelRetriesEnabled,
            TimeSpan? maximumHandlerDuration = null)
        {
            MaximumDeliveryCount = maximumDeliveryCount;
            SecondLevelRetriesEnabled = secondLevelRetriesEnabled;
            MaximumHandlerDuration = maximumHandlerDuration ?? TimeSpan.FromMinutes(1);
        }

        public int MaximumDeliveryCount { get; }

        public bool SecondLevelRetriesEnabled { get; }

        public TimeSpan MaximumHandlerDuration { get; }

        public TimeSpan RetryDelay => TimeSpan.Zero;
    }

    private sealed class MessagingRuntimeContract
    {
        public string Name { get; init; } = string.Empty;

        public byte[] Data { get; init; } = [];
    }

    private sealed class DispatchCommand : ICommand<DispatchCommand>
    {
        public string Value { get; init; } = string.Empty;
    }

    [MessagePackObject(false)]
    public sealed class MessagePackRuntimeContract
    {
        [Key(0)]
        public string Name { get; set; } = string.Empty;
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(MessagingRuntimeContract))]
    [JsonSerializable(typeof(DispatchCommand))]
    private sealed partial class MessagingTestJsonContext : JsonSerializerContext
    {
    }
}
