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

/// <summary>Verifies transport-neutral envelope and codec behavior.</summary>
[TestClass]
public sealed partial class MessagingEnvelopeTests
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
                ["tests.legacy_message"],
                SampleMessageJsonContext.Default.SampleMessage)
        ]);
        var codec = new MessagingEnvelopeCodec(registry, networkIdentity: "tests-network");
        var expected = new SampleMessage { Text = "binary", Data = [0, 1, 255] };

        var envelope = codec.Create(
            expected,
            "tests-network",
            "sender",
            null,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            DateTimeOffset.Parse("2026-08-19T20:00:00+00:00", CultureInfo.InvariantCulture));
        var headers = new Dictionary<string, string>(envelope.Context.Headers, StringComparer.Ordinal)
        {
            [MessagingHeaderNames.ContentEncoding] = "br",
            [MessagingHeaderNames.PayloadAttachmentId] = "opaque-attachment"
        };
        envelope = new MessagingEnvelope(new MessagingEnvelopeContext(headers), envelope.Payload);

        var actual = codec.Decode<SampleMessage>(envelope);

        actual.Text.Should().Be(expected.Text);
        actual.Data.Should().Equal(expected.Data);
        envelope.Context.Headers[MessagingHeaderNames.ContentType].Should().Be(protocol switch
        {
            SerializationProtocol.Json => MessagingContentTypes.Json,
            SerializationProtocol.MessagePack => MessagingContentTypes.MessagePack,
            SerializationProtocol.Protobuf => MessagingContentTypes.Protobuf,
            _ => throw new InvalidOperationException()
        });
        envelope.Context.Headers[MessagingHeaderNames.ContentEncoding].Should().Be("br");
        envelope.Context.Headers[MessagingHeaderNames.PayloadAttachmentId].Should().Be("opaque-attachment");
    }

    [TestMethod]
    public void FormerNameResolvesWithoutLoadingClrTypes()
    {
        var registry = new MessagingContractRegistry(
        [
            new MessagingContractDescriptor<SampleMessage>(
                "tests.current",
                SerializationProtocol.Json,
                ["tests.former"],
                SampleMessageJsonContext.Default.SampleMessage)
        ]);
        var serializer = new MessagingJsonCodec();
        var payload = new ArrayBufferWriter<byte>();
        serializer.Serialize(payload, new SampleMessage { Text = "value", Data = [7] }, SampleMessageJsonContext.Default.SampleMessage);
        var envelope = new MessagingEnvelope(
            new MessagingEnvelopeContext(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MessagingHeaderNames.MessageType] = "tests.former",
                [MessagingHeaderNames.ContentType] = MessagingContentTypes.Json,
                [MessagingHeaderNames.MessageId] = "11111111-1111-1111-1111-111111111111",
                [MessagingHeaderNames.SentTime] = MessagingEnvelope.FormatSentTime(DateTimeOffset.UtcNow),
                [MessagingHeaderNames.Network] = "tests-network",
                [MessagingHeaderNames.SenderIdentity] = "sender"
            }),
            payload.WrittenMemory);

        var result = new MessagingEnvelopeCodec(registry, networkIdentity: "tests-network").Decode<SampleMessage>(envelope);

        result.Text.Should().Be("value");
    }

    [TestMethod]
    public void ForeignNetworkAndUnsupportedContractFailFast()
    {
        var registry = new MessagingContractRegistry(
        [
            new MessagingContractDescriptor<SampleMessage>(
                "tests.current",
                SerializationProtocol.Json,
                jsonTypeInfo: SampleMessageJsonContext.Default.SampleMessage)
        ]);
        var codec = new MessagingEnvelopeCodec(registry, networkIdentity: "tests-network");
        var envelope = codec.Create(new SampleMessage { Text = "value" }, "foreign-network", "sender");

        var action = () => codec.Decode(envelope);

        action.Should().Throw<MessagingEnvelopeException>().Which.Kind.Should().Be(MessagingFailureKind.ForeignNetwork);
    }

    [TestMethod]
    public void PayloadAndHeadersRemainSeparate()
    {
        var context = new MessagingEnvelopeContext(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["transport-delivery-count"] = "1"
        });
        var envelope = new MessagingEnvelope(context, new byte[] { 1, 2, 3 });

        envelope.Context.Headers.Should().ContainKey("transport-delivery-count");
        envelope.Payload.Should().Equal(1, 2, 3);
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
}
