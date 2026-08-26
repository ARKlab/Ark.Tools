// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

using Ark.Tools.MediatorFramework.AzureFunctions;
using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the Azure Functions QueueTrigger settlement adapter.</summary>
[TestClass]
public sealed class MessagingStorageQueueFunctionsTests
{
    [TestMethod]
    public async Task LockedDeliveryMapsCompleteAbandonAndDeadLetter()
    {
        var source = new RecordingQueueClient();
        var poison = new RecordingQueueClient();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingHeaders.MessageType] = "books_print"
        };
        var encoded = StorageQueueEnvelopeCodec.Encode(
            headers,
            new ReadOnlySequence<byte>(new byte[] { 1, 2, 3 }));
        var message = QueuesModelFactory.QueueMessage(
            "native-id",
            "receipt",
            BinaryData.FromString(encoded),
            3,
            insertedOn: null,
            expiresOn: null,
            nextVisibleOn: null);
        var envelope = StorageQueueEnvelopeCodec.Decode(message.Body);
        var delivery = new MessagingQueueFunctionsDispatcher.StorageQueueFunctionsLockedDelivery(
            source,
            poison,
            message,
            envelope);

        delivery.Headers.Should().BeEquivalentTo(headers);
        delivery.Payload.ToArray().Should().Equal(1, 2, 3);
        delivery.DeliveryCount.Should().Be(3);
        await delivery.RenewLockAsync(default).ConfigureAwait(false);
        await delivery.CompleteAsync(default).ConfigureAwait(false);

        var abandon = async () => await delivery.AbandonAsync(default).ConfigureAwait(false);
        await abandon.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*visibility timeout*").ConfigureAwait(false);

        await delivery.DeadLetterAsync("failed", "description", default).ConfigureAwait(false);
        poison.SentBody.Should().NotBeNull();
        StorageQueueEnvelopeCodec.Decode(poison.SentBody!)
            .Headers[StorageQueuePoisonHeaders.OriginalMessageId].Should().Be("native-id");
        source.DeletedMessageId.Should().Be("native-id");
        source.DeletedPopReceipt.Should().Be("receipt");
    }

    [TestMethod]
    public async Task HostSettingsValidatorEnforcesStrictRetryContract()
    {
        var manifest = new MessagingFunctionsManifest(
            typeof(MessagingStorageQueueFunctionsTests),
            typeof(MessagingStorageQueueFunctionsTests),
            MessagingFunctionsTriggerBinding.StorageQueue,
            "printing",
            "BookMessaging",
            6,
            TimeSpan.FromMinutes(2),
            Array.Empty<MessagingFunctionsSubscription>(),
            Array.Empty<Type>(),
            Array.Empty<Type>(),
            TimeSpan.FromSeconds(30),
            strictStorageQueueHostSettings: true);
        var matching = new StorageQueueFunctionsHostSettings(
            "none",
            6,
            TimeSpan.FromSeconds(30));
        var valid = new StorageQueueFunctionsHostSettingsValidator(manifest, matching);

        await valid.StartAsync(default).ConfigureAwait(false);
        await valid.StopAsync(default).ConfigureAwait(false);

        var mismatched = new StorageQueueFunctionsHostSettingsValidator(
            manifest,
            new StorageQueueFunctionsHostSettings("base64", 3, TimeSpan.FromSeconds(1)));
        var act = async () => await mismatched.StartAsync(default).ConfigureAwait(false);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Expected messageEncoding=none*")
            .ConfigureAwait(false);
    }

    private sealed class RecordingQueueClient : QueueClient
    {
        public BinaryData? SentBody { get; private set; }

        public string? DeletedMessageId { get; private set; }

        public string? DeletedPopReceipt { get; private set; }

        public override async Task<Response<SendReceipt>> SendMessageAsync(
            BinaryData message,
            TimeSpan? visibilityTimeout = null,
            TimeSpan? timeToLive = null,
            CancellationToken cancellationToken = default)
        {
            SentBody = message;
            return await Task.FromResult<Response<SendReceipt>>(null!).ConfigureAwait(false);
        }

        public override async Task<Response> DeleteMessageAsync(
            string messageId,
            string popReceipt,
            CancellationToken cancellationToken = default)
        {
            DeletedMessageId = messageId;
            DeletedPopReceipt = popReceipt;
            return await Task.FromResult<Response>(null!).ConfigureAwait(false);
        }
    }
}
