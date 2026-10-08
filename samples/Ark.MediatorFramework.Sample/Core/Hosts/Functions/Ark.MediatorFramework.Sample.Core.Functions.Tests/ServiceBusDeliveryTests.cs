// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;

using AwesomeAssertions;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using NodaTime;

namespace Ark.MediatorFramework.Sample.Core.Functions.Tests;

/// <summary>Delivers an Api message to the worker queue on the Service Bus emulator.</summary>
[TestClass]
[TestCategory("integration")]
[DoNotParallelize]
public sealed class ServiceBusDeliveryTests
{
    private const string _userContextHeaderName = MessagingHeaders.UserId;

    /// <summary>The Api producer commits to the outbox; the outbox processor puts the message on the worker queue.</summary>
    [TestMethod]
    public async Task ApiMessageReachesWorkerQueueThroughTheOutbox()
    {
        var queue = FunctionsTestHosts.WorkerQueueName();
        var administration = new ServiceBusAdministrationClient(ServiceBusEmulator.AdministrationConnectionString);
        await ServiceBusEmulator.RecreateQueueAsync(administration, queue).ConfigureAwait(false);
        try
        {
            await using var client = new ServiceBusClient(ServiceBusEmulator.DataPlaneConnectionString);
            var store = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
            // The in-memory DataBus is enough: the test runs every process in one.
            await using var api = await FunctionsTestHosts.StartApiProducerAsync(
                transport => transport.UseServiceBus(client),
                static dataBus => dataBus.UseInMemory(lifetime: Duration.FromHours(2)),
                store).ConfigureAwait(false);
#pragma warning disable CA2000 // The test owns the Service Bus client the transport wraps.
            await using var outbox = await FunctionsTestHosts.StartOutboxProcessorAsync(
                new ServiceBusMessagingTransport(client), store).ConfigureAwait(false);
#pragma warning restore CA2000

            var book = await api.DispatchAsync<Book_CreateRequest.V1, Book.V1.Output>(FunctionsTestHosts.NewBook())
                .ConfigureAwait(false);
            await api.DispatchAsync<CreateBookPrintProcessRequest.V1, BookPrintProcessResponse>(
                new CreateBookPrintProcessRequest.V1 { BookId = book.Id }).ConfigureAwait(false);

            await using var receiver = client.CreateReceiver(queue);
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            message.Should().NotBeNull();
            message!.ApplicationProperties[MessagingHeaders.MessageType].Should().Be("books_process_book_print_process");
            message.ApplicationProperties.Should().ContainKey(_userContextHeaderName);
        }
        finally
        {
            await administration.DeleteQueueAsync(queue).ConfigureAwait(false);
        }
    }
}
