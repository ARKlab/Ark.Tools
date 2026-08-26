// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

using Ark.Tools.MediatorFramework.AzureFunctions;

using AwesomeAssertions;

using Azure.Messaging.ServiceBus;

using Microsoft.Azure.Functions.Worker;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the Azure Functions Service Bus settlement adapter.</summary>
[TestClass]
public sealed class MessagingServiceBusFunctionsTests
{
    [TestMethod]
    public async Task LockedDeliveryMapsMessageAndSettlementActions()
    {
        var actions = new RecordingMessageActions();
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromBytes(new byte[] { 1, 2, 3 }),
            properties: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["text"] = "value",
                ["number"] = 42
            },
            lockTokenGuid: Guid.NewGuid(),
            deliveryCount: 3);
        var delivery = new ServiceBusMessagingLockedDelivery(message, actions);

        delivery.Headers.Should().BeEquivalentTo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["text"] = "value",
            ["number"] = "42"
        });
        delivery.Payload.ToArray().Should().Equal(1, 2, 3);
        delivery.DeliveryCount.Should().Be(3);

        await delivery.RenewLockAsync(default).ConfigureAwait(false);
        actions.Renewed.Should().BeSameAs(message);
        await delivery.CompleteAsync(default).ConfigureAwait(false);
        actions.Completed.Should().BeSameAs(message);
        await delivery.AbandonAsync(default).ConfigureAwait(false);
        actions.Abandoned.Should().BeSameAs(message);
        await delivery.DeadLetterAsync(new string('r', 300), new string('d', 1_100), default)
            .ConfigureAwait(false);
        actions.DeadLettered.Should().BeSameAs(message);
        actions.DeadLetterReason.Should().HaveLength(256);
        actions.DeadLetterDescription.Should().HaveLength(1_024);
    }

    private sealed class RecordingMessageActions : ServiceBusMessageActions
    {
        public ServiceBusReceivedMessage? Renewed { get; private set; }

        public ServiceBusReceivedMessage? Completed { get; private set; }

        public ServiceBusReceivedMessage? Abandoned { get; private set; }

        public ServiceBusReceivedMessage? DeadLettered { get; private set; }

        public string? DeadLetterReason { get; private set; }

        public string? DeadLetterDescription { get; private set; }

        public override async Task RenewMessageLockAsync(
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken = default)
        {
            Renewed = message;
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public override async Task CompleteMessageAsync(
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken = default)
        {
            Completed = message;
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public override async Task AbandonMessageAsync(
            ServiceBusReceivedMessage message,
            IDictionary<string, object>? propertiesToModify = default,
            CancellationToken cancellationToken = default)
        {
            Abandoned = message;
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public override async Task DeadLetterMessageAsync(
            ServiceBusReceivedMessage message,
            Dictionary<string, object>? propertiesToModify = default,
            string? deadLetterReason = default,
            string? deadLetterErrorDescription = default,
            CancellationToken cancellationToken = default)
        {
            DeadLettered = message;
            DeadLetterReason = deadLetterReason;
            DeadLetterDescription = deadLetterErrorDescription;
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
