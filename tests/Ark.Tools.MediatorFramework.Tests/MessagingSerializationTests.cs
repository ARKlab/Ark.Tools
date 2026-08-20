// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;
using Ark.MediatorFramework.Messaging;

using AwesomeAssertions;

using MessagePack;

using ProtoBuf;

using System.Buffers;
using System.Text.Json.Serialization;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies transport-neutral message metadata and codec behavior.</summary>
[TestClass]
public sealed partial class MessagingSerializationTests
{
    [TestMethod]
    [DataRow(SerializationProtocol.Json)]
    [DataRow(SerializationProtocol.MessagePack)]
    [DataRow(SerializationProtocol.Protobuf)]
    public void RoundTripsEachInstalledProtocol(SerializationProtocol protocol)
    {
        var registry = new MessagingContractRegistry(
        [
            new MessagingContractDescriptor<SampleMessage>(
                "tests.sample_message",
                protocol,
                ["tests.legacy_message"])
        ]);
        var codec = new MessagingMessageCodec(registry, _createSerializers(), networkIdentity: "tests-network");
        var expected = new SampleMessage { Text = "binary", Data = [0, 1, 255] };
        var payload = new ArrayBufferWriter<byte>();

        var context = codec.CreateContext<SampleMessage>(
            "tests-network",
            "sender",
            null,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            DateTimeOffset.Parse("2026-08-19T20:00:00+00:00", CultureInfo.InvariantCulture));
        codec.Serialize(payload, expected);
        var headers = new Dictionary<string, string>(context.Headers, StringComparer.Ordinal)
        {
            [MessagingHeaderNames.ContentEncoding] = "br",
            [MessagingHeaderNames.PayloadAttachmentId] = "opaque-attachment"
        };
        context = new MessagingMessageContext(headers);

        var actual = codec.Deserialize<SampleMessage>(context, new ReadOnlySequence<byte>(payload.WrittenMemory));

        actual.Text.Should().Be(expected.Text);
        actual.Data.Should().Equal(expected.Data);
        context.Headers[MessagingHeaderNames.ContentType].Should().Be(protocol switch
        {
            SerializationProtocol.Json => MessagingContentTypes.Json,
            SerializationProtocol.MessagePack => MessagingContentTypes.MessagePack,
            SerializationProtocol.Protobuf => MessagingContentTypes.Protobuf,
            _ => throw new InvalidOperationException()
        });
        context.Headers[MessagingHeaderNames.ContentEncoding].Should().Be("br");
        context.Headers[MessagingHeaderNames.PayloadAttachmentId].Should().Be("opaque-attachment");
    }

    [TestMethod]
    public void FormerNameResolvesWithoutLoadingClrTypes()
    {
        var registry = new MessagingContractRegistry(
        [
            new MessagingContractDescriptor<SampleMessage>(
                "tests.current",
                SerializationProtocol.Json,
                ["tests.former"])
        ]);
        var serializer = new MessagingJsonCodec(SampleMessageJsonContext.Default.Options);
        var payload = new ArrayBufferWriter<byte>();
        serializer.Serialize(payload, new SampleMessage { Text = "value", Data = [7] });
        var context = new MessagingMessageContext(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MessagingHeaderNames.MessageType] = "tests.former",
                [MessagingHeaderNames.ContentType] = MessagingContentTypes.Json,
                [MessagingHeaderNames.MessageId] = "11111111-1111-1111-1111-111111111111",
                [MessagingHeaderNames.SentTime] = MessagingMessageContext.FormatSentTime(DateTimeOffset.UtcNow),
                [MessagingHeaderNames.Network] = "tests-network",
                [MessagingHeaderNames.SenderIdentity] = "sender"
            });

        var codec = new MessagingMessageCodec(registry, _createSerializers(), networkIdentity: "tests-network");
        var result = codec.Deserialize<SampleMessage>(context, new ReadOnlySequence<byte>(payload.WrittenMemory));

        result.Text.Should().Be("value");
    }

    [TestMethod]
    public void ForeignNetworkAndUnsupportedContractFailFast()
    {
        var registry = new MessagingContractRegistry(
        [
            new MessagingContractDescriptor<SampleMessage>(
                "tests.current",
                SerializationProtocol.Json)
        ]);
        var codec = new MessagingMessageCodec(registry, _createSerializers(), networkIdentity: "tests-network");
        var context = codec.CreateContext<SampleMessage>("foreign-network", "sender");
        var payload = ReadOnlySequence<byte>.Empty;

        var action = () => codec.Deserialize<SampleMessage>(context, payload);

        action.Should().Throw<MessagingProtocolException>().Which.Kind.Should().Be(MessagingFailureKind.ForeignNetwork);
    }

    [TestMethod]
    public void PayloadAndHeadersRemainSeparate()
    {
        var context = new MessagingMessageContext(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["transport-delivery-count"] = "1"
        });
        var payload = new ReadOnlySequence<byte>(new byte[] { 1, 2, 3 });

        context.Headers.Should().ContainKey("transport-delivery-count");
        payload.ToArray().Should().Equal(1, 2, 3);
    }

    [TestMethod]
    public void SerializeRejectsOversizedPayloadWithoutBuffering()
    {
        var registry = new MessagingContractRegistry(
        [
            new MessagingContractDescriptor<SampleMessage>("tests.current", SerializationProtocol.Json)
        ]);
        var codec = new MessagingMessageCodec(
            registry,
            _createSerializers(),
            limits: new MessagingMessageLimits(maximumPayloadLength: 1));
        var payload = new ArrayBufferWriter<byte>();

        var action = () => codec.Serialize(payload, new SampleMessage { Text = "value" });

        action.Should().Throw<MessagingProtocolException>().Which.Kind.Should().Be(MessagingFailureKind.SizeLimit);
    }

    [MessagePackObject(AllowPrivate = true)]
    [ProtoContract]
    internal sealed class SampleMessage
    {
        [Key(0)]
        [ProtoMember(1)]
        public string Text { get; set; } = string.Empty;

        [Key(1)]
        [ProtoMember(2)]
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    [JsonSerializable(typeof(SampleMessage))]
    private sealed partial class SampleMessageJsonContext : JsonSerializerContext;

    private static MessagingSerializerRegistry _createSerializers()
    {
        return new MessagingSerializerRegistry(
        [
            new MessagingJsonCodec(SampleMessageJsonContext.Default.Options),
            new MessagingMessagePackCodec(),
            new MessagingProtobufCodec()
        ]);
    }
}
