// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Google.Protobuf.WellKnownTypes;

using MessagePack;
using MessagePack.Resolvers;

using Microsoft.Extensions.DependencyInjection;

using System.Buffers;
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
        var action = () => MessagingJsonStartupValidation.ValidateContract<MessagingRuntimeContract>(
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
    public void CodecRegistrationInstallsAllDeclaredProtocols()
    {
        using var services = new ServiceCollection()
            .AddArkMessaging()
            .AddMessagePackAndProtobufMessagingCodecs()
            .BuildServiceProvider();

        var registry = services.GetRequiredService<IMessagingCodecRegistry>();

        registry.IsInstalled(SerializationProtocol.Json).Should().BeTrue();
        registry.IsInstalled(SerializationProtocol.MessagePack).Should().BeTrue();
        registry.IsInstalled(SerializationProtocol.Protobuf).Should().BeTrue();
    }

    [TestMethod]
    public async Task PipelineRunsStepsInDeclaredOrderAndProtectsReservedHeaders()
    {
        var order = new List<string>();
        var context = new MessagingOutgoingContext(
            new Dictionary<string, string>(StringComparer.Ordinal),
            "books",
            default);
        var steps = new IMessagingOutgoingStep[]
        {
            new RecordingOutgoingStep("first", order),
            new RecordingOutgoingStep("second", order)
        };

        await MessagingPipelineInvoker.InvokeOutgoingAsync(
            steps,
            context,
            () =>
            {
                order.Add("terminal");
                return Task.CompletedTask;
            }).ConfigureAwait(false);

        order.Should().Equal("first", "second", "terminal");
        var action = () => context.Headers[MessagingHeaders.MessageType] = "spoofed";
        action.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public async Task UserContextStepsRoundTripClaims()
    {
        ClaimsPrincipal? restored = null;
        var outgoing = new MessagingOutgoingContext(
            new Dictionary<string, string>(StringComparer.Ordinal),
            "books",
            default);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Email, "ada@example.test"),
            new Claim(ClaimTypes.Role, "admin")
        ], "test"));
        await new UserContextOutgoingStep(() => principal)
            .ProcessAsync(outgoing, () => Task.CompletedTask).ConfigureAwait(false);

        using var scope = new ServiceCollection().BuildServiceProvider();
        var incoming = new MessagingIncomingContext(outgoing.Headers, default, scope, default);
        await new UserContextIncomingStep(value => restored = value)
            .ProcessAsync(incoming, () => Task.CompletedTask).ConfigureAwait(false);

        restored!.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("42");
        restored.FindFirst(ClaimTypes.Email)!.Value.Should().Be("ada@example.test");
        restored.IsInRole("admin").Should().BeTrue();
    }

    private sealed class RecordingOutgoingStep : IMessagingOutgoingStep
    {
        private readonly string _name;
        private readonly IList<string> _order;

        public RecordingOutgoingStep(string name, IList<string> order)
        {
            _name = name;
            _order = order;
        }

        public async Task ProcessAsync(MessagingOutgoingContext context, Func<Task> next)
        {
            _order.Add(_name);
            await next().ConfigureAwait(false);
        }
    }

    private sealed class MessagingRuntimeContract
    {
        public string Name { get; init; } = string.Empty;

        public byte[] Data { get; init; } = [];
    }

    [MessagePackObject(false)]
    public sealed class MessagePackRuntimeContract
    {
        [Key(0)]
        public string Name { get; set; } = string.Empty;
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(MessagingRuntimeContract))]
    private sealed partial class MessagingTestJsonContext : JsonSerializerContext
    {
    }
}
