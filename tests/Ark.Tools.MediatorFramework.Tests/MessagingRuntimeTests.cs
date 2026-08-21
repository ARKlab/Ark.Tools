// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Google.Protobuf.WellKnownTypes;

using MessagePack;
using MessagePack.Resolvers;

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

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
