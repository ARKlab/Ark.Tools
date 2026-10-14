// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;

using Ark.Tools.Compliance;
using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Azure.Core;
using Azure.Core.Cryptography;
using Azure.Core.Pipeline;
using Azure.Storage;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Specialized;

namespace Ark.Tools.MediatorFramework.Tests;


/// <summary>Runs the transport conformance suite against the repository Azurite instance.</summary>
[TestClass]
[TestCategory("integration")]
public sealed class StorageQueueMessagingTransportConformanceTests : MessagingTransportConformanceTests
{
    [InfrastructureSecret]
    private const string _connectionString = "UseDevelopmentStorage=true";
    private const string _queue = "amf1-azm11-conformance";
    private const string _emptyQueue = "amf1-azm11-empty";
    private readonly QueueServiceClient _service = new(
        _connectionString,
        new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.None
        });

    protected override string QueueName => _queue;

    protected override string EmptyQueueName => _emptyQueue;

    protected override MessagingCapabilities Capabilities =>
        MessagingCapabilities.SendReceive | MessagingCapabilities.ScheduledSend;

    protected override IMessagingTransport CreateTransport()
    {
        return new StorageQueueMessagingTransport(
            _service,
            receiveVisibilityTimeout: TimeSpan.FromSeconds(30),
            retryDelay: TimeSpan.FromMilliseconds(10));
    }

    [TestInitialize]
    public async Task CreateQueues()
    {
        await _recreateAsync(_queue).ConfigureAwait(false);
        await _recreateAsync(_emptyQueue).ConfigureAwait(false);
        await _service.GetQueueClient(_queue + "-poison").DeleteIfExistsAsync().ConfigureAwait(false);
    }

    [TestCleanup]
    public async Task DeleteQueues()
    {
        await _service.GetQueueClient(_queue).DeleteIfExistsAsync().ConfigureAwait(false);
        await _service.GetQueueClient(_emptyQueue).DeleteIfExistsAsync().ConfigureAwait(false);
        await _service.GetQueueClient(_queue + "-poison").DeleteIfExistsAsync().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ScheduledSendUsesInitialVisibilityDelay()
    {
        var transport = CreateTransport();
        var stopwatch = Stopwatch.StartNew();
        await transport.SendAsync(
            _queue,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new ReadOnlySequence<byte>(new byte[] { 1 }),
            DateTimeOffset.UtcNow.AddSeconds(2),
            default).ConfigureAwait(false);
        var delivery = await ((IMessagingMessageSource)transport).ReceiveOneAsync(_queue, TimeSpan.FromSeconds(30))
            .ConfigureAwait(false);

        stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(1));
        await delivery.CompleteAsync(default).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task DeadLetterMovesEnvelopeAndPreservesOriginalMessageId()
    {
        var transport = (StorageQueueMessagingTransport)CreateTransport();
        await transport.EnsureQueueAsync(_queue, 1, _queue, default).ConfigureAwait(false);
        await transport.SendAsync(
            _queue,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new ReadOnlySequence<byte>(new byte[] { 1, 2, 3 }),
            null,
            default).ConfigureAwait(false);
        var delivery = await ((IMessagingMessageSource)transport).ReceiveOneAsync(_queue).ConfigureAwait(false);

        await delivery.DeadLetterAsync("failed", "description", default).ConfigureAwait(false);

        var poison = await _service.GetQueueClient(_queue + "-poison")
            .ReceiveMessageAsync(cancellationToken: default).ConfigureAwait(false);
        poison.Value.Should().NotBeNull();
        var decoded = StorageQueueEnvelopeCodec.Decode(poison.Value.Body);
        decoded.Headers[StorageQueuePoisonHeaders.Reason].Should().Be("failed");
        decoded.Headers[StorageQueuePoisonHeaders.OriginalMessageId].Should().NotBeNullOrEmpty();
        decoded.Payload.ToArray().Should().Equal(1, 2, 3);
        var original = await _service.GetQueueClient(_queue)
            .ReceiveMessageAsync(cancellationToken: default).ConfigureAwait(false);
        original.Value.Should().BeNull();
    }

    [TestMethod]
    public async Task RenewAndAbandonKeepTheMessageOnTransportBuiltClient()
    {
        // The connection-string transport builds its own client, so renewal and abandon update only the visibility.
        var transport = new StorageQueueMessagingTransport(
            _connectionString,
            receiveVisibilityTimeout: TimeSpan.FromSeconds(30),
            retryDelay: TimeSpan.FromMilliseconds(10));

        var second = await _renewAbandonAndReceiveAgainAsync(transport).ConfigureAwait(false);

        second.DeliveryCount.Should().Be(2);
        second.Headers.Should().Contain("key", "value");
        second.Payload.ToArray().Should().Equal(4, 5, 6);
        await second.CompleteAsync(default).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task RenewAndAbandonResendTheMessageOnEncryptedApplicationClient()
    {
        var updates = new UpdateMessageRecorder();
        var options = new SpecializedQueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.None,
            ClientSideEncryption = new ClientSideEncryptionOptions(ClientSideEncryptionVersion.V2_0)
            {
                KeyEncryptionKey = new TestKeyEncryptionKey(),
                KeyWrapAlgorithm = TestKeyEncryptionKey.Algorithm
            }
        };
        options.AddPolicy(updates, HttpPipelinePosition.PerCall);
        var transport = new StorageQueueMessagingTransport(
            new QueueServiceClient(_connectionString, options),
            receiveVisibilityTimeout: TimeSpan.FromSeconds(30),
            retryDelay: TimeSpan.FromMilliseconds(10));

        var second = await _renewAbandonAndReceiveAgainAsync(transport).ConfigureAwait(false);

        updates.SentContent.Should().Equal(true, true);
        second.DeliveryCount.Should().Be(2);
        second.Headers.Should().Contain("key", "value");
        second.Payload.ToArray().Should().Equal(4, 5, 6);
        await second.CompleteAsync(default).ConfigureAwait(false);
    }

    private static async Task<IMessagingLockedDelivery> _renewAbandonAndReceiveAgainAsync(
        StorageQueueMessagingTransport transport)
    {
        var source = (IMessagingMessageSource)transport;
        await transport.SendAsync(
            _queue,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = "value" },
            new ReadOnlySequence<byte>(new byte[] { 4, 5, 6 }),
            null,
            default).ConfigureAwait(false);
        var first = await source.ReceiveOneAsync(_queue).ConfigureAwait(false);

        await first.RenewLockAsync(default).ConfigureAwait(false);
        await first.AbandonAsync(default).ConfigureAwait(false);
        return await source.ReceiveOneAsync(_queue, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
    }

    private async Task _recreateAsync(string queue)
    {
        var client = _service.GetQueueClient(queue);
        await client.DeleteIfExistsAsync().ConfigureAwait(false);
        await client.CreateIfNotExistsAsync().ConfigureAwait(false);
    }

    // Update Message is the only PUT that carries a pop receipt.
    private sealed class UpdateMessageRecorder : HttpPipelineSynchronousPolicy
    {
        public ConcurrentQueue<bool> SentContent { get; } = new();

        public override void OnSendingRequest(HttpMessage message)
        {
            if (message.Request.Method == RequestMethod.Put
                && message.Request.Uri.Query.Contains("popreceipt=", StringComparison.OrdinalIgnoreCase))
            {
                SentContent.Enqueue(message.Request.Content is not null);
            }
        }
    }

    // Test double only: the "wrapped" key is the key itself, which is enough to exercise client-side encryption.
    private sealed class TestKeyEncryptionKey : IKeyEncryptionKey
    {
        public const string Algorithm = "test-identity";

        public string KeyId => "test-kek";

        public byte[] WrapKey(string algorithm, ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default)
        {
            return key.ToArray();
        }

        public async Task<byte[]> WrapKeyAsync(
            string algorithm,
            ReadOnlyMemory<byte> key,
            CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(key.ToArray()).ConfigureAwait(false);
        }

        public byte[] UnwrapKey(
            string algorithm,
            ReadOnlyMemory<byte> encryptedKey,
            CancellationToken cancellationToken = default)
        {
            return encryptedKey.ToArray();
        }

        public async Task<byte[]> UnwrapKeyAsync(
            string algorithm,
            ReadOnlyMemory<byte> encryptedKey,
            CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(encryptedKey.ToArray()).ConfigureAwait(false);
        }
    }
}
