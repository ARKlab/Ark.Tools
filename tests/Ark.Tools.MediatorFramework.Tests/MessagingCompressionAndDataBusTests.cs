// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using NodaTime;
using NodaTime.Testing;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies payload compression and transport-neutral claim-check behavior.</summary>
[TestClass]
public sealed class MessagingCompressionAndDataBusTests
{
    [TestMethod]
    [DataRow(CompressionAlgorithm.Gzip, "gzip")]
    [DataRow(CompressionAlgorithm.Brotli, "br")]
    public async Task CompressionRoundTripsThroughHeaderDrivenReceiver(
        CompressionAlgorithm algorithm,
        string encoding)
    {
        var network = _network(maxDecompressed: 10_000);
        var dataBus = new InMemoryMessagingDataBus();
        var sender = new MessagingPayloadSender(dataBus, network, algorithm, 1);
        var receiver = new MessagingPayloadReceiver(dataBus, network);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var message = new PayloadContract(new string('a', 256));

        var payload = await sender.BuildOutgoingPayloadAsync(
            message,
            new TextCodec(),
            new InMemoryMessagingTransport(),
            headers,
            default).ConfigureAwait(false);
        var prepared = await receiver.PreparePayloadAsync(headers, payload, default).ConfigureAwait(false);

        headers[MessagingHeaders.ContentEncoding].Should().Be(encoding);
        new TextCodec().Deserialize<PayloadContract>(prepared).Value.Should().Be(message.Value);
    }

    [TestMethod]
    public async Task SmallPayloadOmitsCompressionHeader()
    {
        var network = _network(maxDecompressed: 10_000);
        var dataBus = new InMemoryMessagingDataBus();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);

        var payload = await new MessagingPayloadSender(dataBus, network, CompressionAlgorithm.Gzip, 1_000)
            .BuildOutgoingPayloadAsync(
                new PayloadContract("small"),
                new TextCodec(),
                new InMemoryMessagingTransport(),
                headers,
                default).ConfigureAwait(false);

        payload.Length.Should().BeGreaterThan(0);
        headers.Should().NotContainKey(MessagingHeaders.ContentEncoding);
    }

    [TestMethod]
    public async Task ClaimCheckStoresCompressedBytesAndRoundTrips()
    {
        var network = _network(offloadThreshold: 10, maxDecompressed: 10_000);
        var dataBus = new InMemoryMessagingDataBus();
        var sender = new MessagingPayloadSender(dataBus, network, CompressionAlgorithm.Gzip, 1);
        var receiver = new MessagingPayloadReceiver(dataBus, network);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);

        var payload = await sender.BuildOutgoingPayloadAsync(
            new PayloadContract(new string('a', 1_000)),
            new TextCodec(),
            new InMemoryMessagingTransport(),
            headers,
            default).ConfigureAwait(false);
        var prepared = await receiver.PreparePayloadAsync(headers, payload, default).ConfigureAwait(false);

        payload.IsEmpty.Should().BeTrue();
        dataBus.Count.Should().Be(1);
        int.Parse(
                headers[MessagingHeaders.PayloadAttachmentLength],
                CultureInfo.InvariantCulture)
            .Should().BeLessThan(1_000);
        new TextCodec().Deserialize<PayloadContract>(prepared).Value.Should().Be(new string('a', 1_000));
    }

    [TestMethod]
    public async Task ClaimCheckUsesNativeEnvelopeMeasurement()
    {
        var network = _network(offloadThreshold: 10_000, maxDecompressed: 10_000);
        var dataBus = new InMemoryMessagingDataBus();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var transport = new CappedTransport(300);

        var payload = await new MessagingPayloadSender(dataBus, network, CompressionAlgorithm.None, 0)
            .BuildOutgoingPayloadAsync(
                new PayloadContract("payload"),
                new TextCodec(),
                transport,
                headers,
                default).ConfigureAwait(false);

        payload.IsEmpty.Should().BeTrue();
        headers.Should().ContainKey(MessagingHeaders.PayloadAttachmentId);
    }

    [TestMethod]
    public async Task InvalidAttachmentMetadataAndExpiryFailFast()
    {
        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var dataBus = new InMemoryMessagingDataBus(clock, Duration.FromMinutes(1));
        var network = _network(offloadThreshold: 1, maxDecompressed: 10_000);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var payload = await new MessagingPayloadSender(dataBus, network, CompressionAlgorithm.None, 0)
            .BuildOutgoingPayloadAsync(
                new PayloadContract(new string('b', 100)),
                new TextCodec(),
                new InMemoryMessagingTransport(),
                headers,
                default).ConfigureAwait(false);
        var receiver = new MessagingPayloadReceiver(dataBus, network);

        var wrongLength = new Dictionary<string, string>(headers, StringComparer.Ordinal)
        {
            [MessagingHeaders.PayloadAttachmentLength] = "1"
        };
        var wrongLengthAction = () => receiver.PreparePayloadAsync(wrongLength, payload, default);
        (await wrongLengthAction
            .Should().ThrowAsync<MessagingFailFastException>().ConfigureAwait(false))
            .Which.Reason.Should().Be(MessagingFailFastReason.AttachmentIntegrityFailure);

        clock.Advance(Duration.FromMinutes(1));
        var expiredAction = () => receiver.PreparePayloadAsync(headers, payload, default);
        (await expiredAction
            .Should().ThrowAsync<MessagingFailFastException>().ConfigureAwait(false))
            .Which.Reason.Should().Be(MessagingFailFastReason.AttachmentIntegrityFailure);
    }

    [TestMethod]
    public async Task DecompressionHonorsMaximumOutput()
    {
        var network = _network(maxDecompressed: 4);
        var dataBus = new InMemoryMessagingDataBus();
        var sender = new MessagingPayloadSender(dataBus, network, CompressionAlgorithm.Gzip, 1);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var payload = await sender.BuildOutgoingPayloadAsync(
            new PayloadContract("payload"),
            new TextCodec(),
            new InMemoryMessagingTransport(),
            headers,
            default).ConfigureAwait(false);
        var receiver = new MessagingPayloadReceiver(dataBus, network);

        var action = () => receiver.PreparePayloadAsync(headers, payload, default);
        (await action
            .Should().ThrowAsync<MessagingFailFastException>().ConfigureAwait(false))
            .Which.Reason.Should().Be(MessagingFailFastReason.OversizedPayload);
    }

    private static MessagingNetworkOptions _network(
        int offloadThreshold = 200_000,
        int maxDecompressed = 1_000_000)
    {
        return new MessagingNetworkOptions(
            typeof(MessagingCompressionAndDataBusTests),
            new MessagingNetworkAttribute
            {
                DataBusOffloadThresholdBytes = offloadThreshold,
                DataBusMaximumAttachmentBytes = 50_000,
                MaximumDecompressedPayloadBytes = maxDecompressed
            });
    }

    private sealed record PayloadContract(string Value);

    private sealed class TextCodec : IMessagingCodec
    {
        public string ContentType => "text/plain";
        public SerializationProtocol Protocol => SerializationProtocol.Json;

        public void Serialize<T>(T value, IBufferWriter<byte> writer)
            where T : class
        {
            var contract = (PayloadContract)(object)value;
            var bytes = Encoding.UTF8.GetBytes(contract.Value);
            bytes.CopyTo(writer.GetSpan(bytes.Length));
            writer.Advance(bytes.Length);
        }

        public T Deserialize<T>(in ReadOnlySequence<byte> payload)
            where T : class
        {
            return (T)(object)new PayloadContract(Encoding.UTF8.GetString(payload.ToArray()));
        }
    }

    private sealed class CappedTransport : IMessagingTransport
    {
        public CappedTransport(long ceiling)
        {
            MaximumInlineEnvelopeBytes = ceiling;
        }

        public MessagingCapabilities Capabilities => MessagingCapabilities.None;
        public long? MaximumInlineEnvelopeBytes { get; }

        public long MeasureNative(
            IReadOnlyDictionary<string, string> headers,
            in ReadOnlySequence<byte> payload)
        {
            return headers.Sum(pair => pair.Key.Length + pair.Value.Length)
                + (payload.IsEmpty ? 0 : 500)
                + payload.Length;
        }

        public async Task SendAsync(
            string queue,
            IReadOnlyDictionary<string, string> headers,
            ReadOnlySequence<byte> payload,
            DateTimeOffset? dueTime,
            CancellationToken ctk)
        {
            await Task.CompletedTask;
        }

        public async Task PublishAsync(
            string topic,
            IReadOnlyDictionary<string, string> headers,
            ReadOnlySequence<byte> payload,
            CancellationToken ctk)
        {
            await Task.CompletedTask;
        }
    }
}
