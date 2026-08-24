// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Security.Cryptography;

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

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
        var prepared = await receiver
            .PreparePayloadAsync(headers, payload, default)
            .ConfigureAwait(false);
        await using (prepared.ConfigureAwait(false))
        {
            headers[MessagingHeaders.ContentEncoding].Should().Be(encoding);
            using var reader = new StreamReader(prepared, Encoding.UTF8);
            (await reader.ReadToEndAsync().ConfigureAwait(false)).Should().Be(message.Value);
        }
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
        var transport = new InMemoryMessagingTransport();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);

        var payload = await sender.BuildOutgoingPayloadAsync(
            new PayloadContract(new string('a', 1_000)),
            new TextCodec(),
            transport,
            headers,
            default).ConfigureAwait(false);
        await transport.SendAsync("payloads", headers, payload, null, default).ConfigureAwait(false);
        await using var deliveries = transport
            .ReceiveAsync("payloads", default)
            .ConfigureAwait(false)
            .GetAsyncEnumerator();
        (await deliveries.MoveNextAsync()).Should().BeTrue();
        var delivery = deliveries.Current;
        var prepared = await receiver
            .PreparePayloadAsync(delivery.Headers, delivery.Payload, default)
            .ConfigureAwait(false);
        await using (prepared.ConfigureAwait(false))
        {
            payload.IsEmpty.Should().BeTrue();
            dataBus.Count.Should().Be(1);
            int.Parse(
                    delivery.Headers[MessagingHeaders.PayloadAttachmentLength],
                    CultureInfo.InvariantCulture)
                .Should().BeLessThan(1_000);
            using var reader = new StreamReader(prepared, Encoding.UTF8);
            (await reader.ReadToEndAsync().ConfigureAwait(false)).Should().Be(new string('a', 1_000));
        }
        await delivery.CompleteAsync(default).ConfigureAwait(false);
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
    public async Task PayloadAboveNetworkLimitUsesClaimCheck()
    {
        var network = _network(
            offloadThreshold: 1_000,
            maximumTransportPayload: 10,
            maxDecompressed: 10_000);
        var dataBus = new InMemoryMessagingDataBus();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);

        var payload = await new MessagingPayloadSender(dataBus, network, CompressionAlgorithm.None, 0)
            .BuildOutgoingPayloadAsync(
                new PayloadContract(new string('a', 100)),
                new TextCodec(),
                new InMemoryMessagingTransport(),
                headers,
                default).ConfigureAwait(false);

        payload.IsEmpty.Should().BeTrue();
        dataBus.Count.Should().Be(1);
    }

    [TestMethod]
    public async Task PayloadSenderCanWriteReservedHeadersThroughOutgoingContext()
    {
        var network = _network(maxDecompressed: 10_000);
        var dataBus = new InMemoryMessagingDataBus();
        var context = new MessagingOutgoingContext(
            new Dictionary<string, string>(StringComparer.Ordinal),
            "books");

        var payload = await new MessagingPayloadSender(dataBus, network, CompressionAlgorithm.None, 0)
            .BuildOutgoingPayloadAsync(
                new PayloadContract("payload"),
                new TextCodec(),
                new InMemoryMessagingTransport(),
                context.Headers,
                default).ConfigureAwait(false);

        payload.IsEmpty.Should().BeFalse();
        context.Headers[MessagingHeaders.ContentType].Should().Be("text/plain");
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
    public async Task MissingAttachmentFailsIntegrity()
    {
        var network = _network(maxDecompressed: 10_000);
        var receiver = new MessagingPayloadReceiver(new InMemoryMessagingDataBus(), network);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingHeaders.PayloadAttachmentId] = "missing",
            [MessagingHeaders.PayloadAttachmentLength] = "1",
            [MessagingHeaders.PayloadAttachmentSha256] = new string('0', 64)
        };

        var action = () => receiver.PreparePayloadAsync(headers, default, default);
        (await action.Should().ThrowAsync<MessagingFailFastException>().ConfigureAwait(false))
            .Which.Reason.Should().Be(MessagingFailFastReason.AttachmentIntegrityFailure);
    }

    [TestMethod]
    public async Task AttachmentDigestMismatchFailsIntegrity()
    {
        var network = _network(offloadThreshold: 1, maxDecompressed: 10_000);
        var dataBus = new InMemoryMessagingDataBus();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var payload = await new MessagingPayloadSender(dataBus, network, CompressionAlgorithm.None, 0)
            .BuildOutgoingPayloadAsync(
                new PayloadContract("payload"),
                new TextCodec(),
                new InMemoryMessagingTransport(),
                headers,
                default).ConfigureAwait(false);
        headers[MessagingHeaders.PayloadAttachmentSha256] =
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("different")));
        var receiver = new MessagingPayloadReceiver(dataBus, network);

        var action = () => receiver.PreparePayloadAsync(headers, payload, default);
        (await action.Should().ThrowAsync<MessagingFailFastException>().ConfigureAwait(false))
            .Which.Reason.Should().Be(MessagingFailFastReason.AttachmentIntegrityFailure);
    }

    [TestMethod]
    public async Task SharedAttachmentRemainsReadableAcrossDeliveries()
    {
        var network = _network(offloadThreshold: 1, maxDecompressed: 10_000);
        var dataBus = new InMemoryMessagingDataBus();
        var sender = new MessagingPayloadSender(dataBus, network, CompressionAlgorithm.None, 0);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var payload = await sender.BuildOutgoingPayloadAsync(
            new PayloadContract("payload"),
            new TextCodec(),
            new InMemoryMessagingTransport(),
            headers,
            default).ConfigureAwait(false);
        var receiver = new MessagingPayloadReceiver(dataBus, network);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var prepared = await receiver
                .PreparePayloadAsync(headers, payload, default)
                .ConfigureAwait(false);
            await using (prepared.ConfigureAwait(false))
            {
                using var reader = new StreamReader(prepared, Encoding.UTF8);
                (await reader.ReadToEndAsync().ConfigureAwait(false)).Should().Be("payload");
            }
        }

        dataBus.Count.Should().Be(1);
    }

    [TestMethod]
    public async Task StorePurgesExpiredAttachments()
    {
        var clock = new FakeClock(Instant.FromUtc(2024, 1, 1, 0, 0));
        var dataBus = new InMemoryMessagingDataBus(clock, Duration.FromMinutes(1));
        await dataBus.StoreAsync(
            new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("expired")),
            default).ConfigureAwait(false);
        clock.Advance(Duration.FromMinutes(1));

        await dataBus.StoreAsync(
            new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("current")),
            default).ConfigureAwait(false);

        dataBus.Count.Should().Be(1);
    }

    [TestMethod]
    public async Task PartialOrMalformedAttachmentMetadataFailsFast()
    {
        var network = _network(maxDecompressed: 10_000);
        var receiver = new MessagingPayloadReceiver(new InMemoryMessagingDataBus(), network);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingHeaders.PayloadAttachmentId] = "attachment",
            [MessagingHeaders.PayloadAttachmentLength] = "1",
            [MessagingHeaders.PayloadAttachmentSha256] = "not-a-sha256"
        };

        foreach (var key in metadata.Keys.ToArray())
        {
            var partial = new Dictionary<string, string>(metadata, StringComparer.Ordinal);
            partial.Remove(key);
            var action = () => receiver.PreparePayloadAsync(partial, default, default);
            (await action.Should().ThrowAsync<MessagingFailFastException>().ConfigureAwait(false))
                .Which.Reason.Should().Be(MessagingFailFastReason.MalformedHeaders);
        }

        var malformed = () => receiver.PreparePayloadAsync(metadata, default, default);
        (await malformed.Should().ThrowAsync<MessagingFailFastException>().ConfigureAwait(false))
            .Which.Reason.Should().Be(MessagingFailFastReason.AttachmentIntegrityFailure);
    }

    [TestMethod]
    public async Task StreamPayloadReaderBridgesPreparedPayloadToGeneratedDispatch()
    {
        var network = _network(maxDecompressed: 10_000);
        var dataBus = new InMemoryMessagingDataBus();
        var sender = new MessagingPayloadSender(dataBus, network, CompressionAlgorithm.Gzip, 1);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var payload = await sender.BuildOutgoingPayloadAsync(
            new PayloadContract(new string('a', 100)),
            new TextCodec(),
            new InMemoryMessagingTransport(),
            headers,
            default).ConfigureAwait(false);
        var receiver = new MessagingPayloadReceiver(dataBus, network);

        var reader = await receiver
            .PreparePayloadReaderAsync(headers, payload, new TextCodec(), default)
            .ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            reader.Deserialize<PayloadContract>().Value.Should().Be(new string('a', 100));
        }
    }

    [TestMethod]
    public void InMemoryDataBusLifetimeMustCoverSchedulingDelay()
    {
        var network = _network();
        var shortLifetime = () => new ServiceCollection()
            .AddArkInMemoryMessagingDataBus(networks: [network]);
        shortLifetime.Should().Throw<ArgumentOutOfRangeException>();

        var longLifetime = () => new ServiceCollection()
            .AddArkInMemoryMessagingDataBus(
                lifetime: Duration.FromDays(8),
                networks: [network]);
        longLifetime.Should().NotThrow();
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

        var prepared = await receiver
            .PreparePayloadAsync(headers, payload, default)
            .ConfigureAwait(false);
        await using (prepared.ConfigureAwait(false))
        {
            Func<Task> action = () => prepared.CopyToAsync(Stream.Null);
            (await action
                .Should().ThrowAsync<MessagingFailFastException>().ConfigureAwait(false))
                .Which.Reason.Should().Be(MessagingFailFastReason.OversizedPayload);
        }
    }

    [TestMethod]
    public async Task AttachmentPayloadIsReadLazilyThroughReturnedStream()
    {
        var content = Encoding.UTF8.GetBytes("payload");
        var source = new TrackingReadStream(content);
        var dataBus = new TrackingDataBus(source);
        var network = _network(maxDecompressed: 100);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingHeaders.PayloadAttachmentId] = "attachment",
            [MessagingHeaders.PayloadAttachmentLength] =
                content.Length.ToString(CultureInfo.InvariantCulture),
            [MessagingHeaders.PayloadAttachmentSha256] =
                Convert.ToHexString(SHA256.HashData(content))
        };

        var receiver = new MessagingPayloadReceiver(dataBus, network);
        var payload = await receiver
            .PreparePayloadAsync(headers, ReadOnlySequence<byte>.Empty, default)
            .ConfigureAwait(false);
        await using (payload.ConfigureAwait(false))
        {
            source.ReadCount.Should().Be(0);
            using var reader = new StreamReader(payload, Encoding.UTF8);
            (await reader.ReadToEndAsync().ConfigureAwait(false)).Should().Be("payload");
        }

        source.ReadCount.Should().BeGreaterThan(0);
    }

    private static MessagingNetworkOptions _network(
        int offloadThreshold = 200_000,
        int maxDecompressed = 1_000_000,
        int maximumTransportPayload = 240_000)
    {
        return new MessagingNetworkOptions(
            typeof(MessagingCompressionAndDataBusTests),
            new MessagingNetworkAttribute
            {
                DataBusOffloadThresholdBytes = offloadThreshold,
                DataBusMaximumAttachmentBytes = 50_000,
                MaximumTransportPayloadBytes = maximumTransportPayload,
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

    private sealed class TrackingDataBus : IMessagingDataBus
    {
        private readonly Stream _stream;

        public TrackingDataBus(Stream stream)
        {
            _stream = stream;
        }

        public Task<string> StoreAsync(ReadOnlySequence<byte> content, CancellationToken ctk)
        {
            throw new NotSupportedException();
        }

        public Task<Stream> OpenReadAsync(
            string attachmentId,
            long expectedLength,
            string expectedSha256,
            CancellationToken ctk)
        {
            ctk.ThrowIfCancellationRequested();
            return Task.FromResult(_stream);
        }
    }

    private sealed class TrackingReadStream : MemoryStream
    {
        public TrackingReadStream(byte[] content)
            : base(content, writable: false)
        {
        }

        public int ReadCount { get; private set; }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public override int Read(Span<byte> buffer)
        {
            ReadCount++;
            return base.Read(buffer);
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
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task PublishAsync(
            string topic,
            IReadOnlyDictionary<string, string> headers,
            ReadOnlySequence<byte> payload,
            CancellationToken ctk)
        {
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
