// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Buffers.Text;

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the Azure Storage Queue wire envelope and transport capability contract.</summary>
[TestClass]
public sealed class StorageQueueEnvelopeCodecTests
{
    [TestMethod]
    public void EnvelopeRoundTripsOpaqueBinaryPayloadAndHeaders()
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingHeaders.MessageType] = "books_print",
            [MessagingHeaders.ContentType] = "application/x-protobuf",
            ["unicode"] = "libro-è"
        };
        var payload = new ReadOnlySequence<byte>(
            new byte[] { 0, 255, 1, 128, 13, 10, 0 });

        var encoded = StorageQueueEnvelopeCodec.Encode(headers, payload);
        var decoded = StorageQueueEnvelopeCodec.Decode(BinaryData.FromString(encoded));

        decoded.Headers.Should().BeEquivalentTo(headers);
        decoded.Payload.ToArray().Should().Equal(payload.ToArray());
        Convert.TryFromBase64String(encoded, new byte[encoded.Length], out _).Should().BeTrue();
    }

    [TestMethod]
    public void EnvelopeHonorsNormalAndFinalEncodedBoundaries()
    {
        var maximumPayload = new byte[StorageQueueLimits.MaximumNormalCanonicalBytes - 1];
        var encoded = StorageQueueEnvelopeCodec.Encode(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new ReadOnlySequence<byte>(maximumPayload));

        Encoding.UTF8.GetByteCount(encoded).Should().Be(
            Base64.GetMaxEncodedToUtf8Length(StorageQueueLimits.MaximumNormalCanonicalBytes));
        Encoding.UTF8.GetByteCount(encoded).Should().BeLessThan(
            StorageQueueLimits.MaximumEncodedTextBytes);

        var oversizedPayload = new byte[StorageQueueLimits.MaximumNormalCanonicalBytes];
        var act = () => StorageQueueEnvelopeCodec.Encode(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new ReadOnlySequence<byte>(oversizedPayload));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void TransportMeasuresCompleteBase64EncodedEnvelope()
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingHeaders.MessageType] = "books_print"
        };
        var payload = new ReadOnlySequence<byte>(new byte[1024]);
        var transport = new StorageQueueMessagingTransport("UseDevelopmentStorage=true");
        var encoded = StorageQueueEnvelopeCodec.Encode(headers, payload);

        transport.MeasureNativePayload(headers, payload).Should().Be(Encoding.UTF8.GetByteCount(encoded));
        transport.MeasureNativePayload(headers, payload).Should().BeGreaterThan(
            transport.MeasureNativeHeaders(headers) + payload.Length);
    }

    [TestMethod]
    public void EnvelopeRejectsMalformedBase64AndDuplicateHeaders()
    {
        var malformed = () => StorageQueueEnvelopeCodec.Decode(BinaryData.FromString("not-base64"));
        malformed.Should().Throw<MessagingFailFastException>()
            .Which.Reason.Should().Be(MessagingFailFastReason.MalformedHeaders);

        var duplicateCanonical = new byte[]
        {
            2,
            1, (byte)'a', 1, (byte)'1',
            1, (byte)'a', 1, (byte)'2'
        };
        var duplicate = () => StorageQueueEnvelopeCodec.Decode(
            BinaryData.FromString(Convert.ToBase64String(duplicateCanonical)));
        duplicate.Should().Throw<MessagingFailFastException>()
            .Which.Reason.Should().Be(MessagingFailFastReason.MalformedHeaders);
    }

    [TestMethod]
    public async Task TransportDeclaresCapabilitiesAndRejectsPubSub()
    {
        var transport = new StorageQueueMessagingTransport("UseDevelopmentStorage=true");

        transport.Capabilities.Should().Be(
            MessagingCapabilities.SendReceive | MessagingCapabilities.ScheduledSend);
        transport.MaximumPayloadBytes.Should().Be(48 * 1024);
        var network = new MessagingNetworkOptions(
            typeof(StorageQueueEnvelopeCodecTests),
            new MessagingNetworkAttribute
            {
                Requires = MessagingCapabilities.PubSub
            });
        var validation = () => network.Validate(transport.Capabilities);
        validation.Should().Throw<InvalidOperationException>()
            .WithMessage("*PubSub*");

        var publish = async () => await transport.PublishAsync(
            "topic",
            new Dictionary<string, string>(StringComparer.Ordinal),
            ReadOnlySequence<byte>.Empty,
            default).ConfigureAwait(false);
        await publish.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*PubSub*").ConfigureAwait(false);

        var management = (IMessagingTransportManagement)transport;
        var ensureTopic = async () =>
            await management.EnsureTopicAsync("topic", "publisher", default).ConfigureAwait(false);
        await ensureTopic.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*PubSub*").ConfigureAwait(false);
    }
}
