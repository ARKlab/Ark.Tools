// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;
using Ark.MediatorFramework.Messaging;

using AwesomeAssertions;

using MessagePack;

using ProtoBuf;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies transport-neutral envelope and codec behavior.</summary>
[TestClass]
public sealed class MessagingEnvelopeTests
{
    [TestMethod]
    [DataRow(SerializationProtocol.Json)]
    [DataRow(SerializationProtocol.MessagePack)]
    [DataRow(SerializationProtocol.Protobuf)]
    public void RoundTripsEachInstalledProtocol(SerializationProtocol protocol)
    {
        var registry = new MessagingContractRegistry(
        [
            new MessagingContractDescriptor(
                typeof(SampleMessage),
                "tests.sample_message",
                protocol,
                ["tests.legacy_message"])
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
        var headers = new Dictionary<string, string>(envelope.Headers, StringComparer.Ordinal)
        {
            [MessagingHeaderNames.ContentEncoding] = "br",
            [MessagingHeaderNames.PayloadAttachmentId] = "opaque-attachment"
        };
        envelope = new MessagingEnvelope(envelope.Payload, headers);

        var actual = codec.Decode<SampleMessage>(envelope);

        actual.Text.Should().Be(expected.Text);
        actual.Data.Should().Equal(expected.Data);
        envelope.Headers[MessagingHeaderNames.ContentType].Should().Be(protocol switch
        {
            SerializationProtocol.Json => MessagingContentTypes.Json,
            SerializationProtocol.MessagePack => MessagingContentTypes.MessagePack,
            SerializationProtocol.Protobuf => MessagingContentTypes.Protobuf,
            _ => throw new InvalidOperationException()
        });
        envelope.Headers[MessagingHeaderNames.ContentEncoding].Should().Be("br");
        envelope.Headers[MessagingHeaderNames.PayloadAttachmentId].Should().Be("opaque-attachment");
    }

    [TestMethod]
    public void FormerNameResolvesWithoutLoadingClrTypes()
    {
        var registry = new MessagingContractRegistry(
        [
            new MessagingContractDescriptor(
                typeof(SampleMessage),
                "tests.current",
                SerializationProtocol.Json,
                ["tests.former"])
        ]);
        var serializer = new MessagingJsonCodec();
        var payload = serializer.Serialize(typeof(SampleMessage), new SampleMessage { Text = "value", Data = [7] });
        var envelope = new MessagingEnvelope(
            payload,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MessagingHeaderNames.MessageType] = "tests.former",
                [MessagingHeaderNames.ContentType] = MessagingContentTypes.Json,
                [MessagingHeaderNames.MessageId] = "11111111-1111-1111-1111-111111111111",
                [MessagingHeaderNames.SentTime] = MessagingEnvelope.FormatSentTime(DateTimeOffset.UtcNow),
                [MessagingHeaderNames.Network] = "tests-network",
                [MessagingHeaderNames.SenderIdentity] = "sender"
            });

        var result = new MessagingEnvelopeCodec(registry, networkIdentity: "tests-network").Decode<SampleMessage>(envelope);

        result.Text.Should().Be("value");
    }

    [TestMethod]
    public void ForeignNetworkAndUnsupportedContractFailFast()
    {
        var registry = new MessagingContractRegistry(
        [
            new MessagingContractDescriptor(typeof(SampleMessage), "tests.current", SerializationProtocol.Json)
        ]);
        var codec = new MessagingEnvelopeCodec(registry, networkIdentity: "tests-network");
        var envelope = codec.Create(new SampleMessage { Text = "value" }, "foreign-network", "sender");

        var action = () => codec.Decode(envelope);

        action.Should().Throw<MessagingEnvelopeException>().Which.Kind.Should().Be(MessagingFailureKind.ForeignNetwork);
    }

    [TestMethod]
    public void DeliveryCountCannotBeCarriedByEnvelope()
    {
        var action = () => new MessagingEnvelope(
            Array.Empty<byte>(),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MessagingHeaderNames.RebusDeliveryCount] = "1"
            });

        action.Should().Throw<MessagingEnvelopeException>().Which.Kind.Should().Be(MessagingFailureKind.Malformed);
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
}
