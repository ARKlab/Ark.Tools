// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application.JsonContext;
using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies the sample's generated restricted bus composition.</summary>
[TestClass]
public sealed class MessagingBusSampleTests
{
    [TestMethod]
    public async Task SendRoutesBookPrintMessageToSampleParticipant()
    {
        var network = SampleMessagingNetwork.CreateOptions();
        var transport = new InMemoryMessagingTransport();
        var dataBus = new InMemoryMessagingDataBus();
        var codec = new JsonMessagingCodec(new JsonSerializerOptions
        {
            TypeInfoResolver = ApplicationJsonSerializerContext.Default
        });
        using var bus = new MessagingBus(
            transport,
            network,
            SampleMessagingNetwork.CreateRegistry(),
            new MessagingCodecRegistry([codec]),
            SampleMessagingParticipant.CreatePayloadSender(dataBus, network),
            SampleMessagingParticipant.Identity);

        await bus.Send(new ProcessBookPrintProcessRequest { Id = Guid.NewGuid() }).ConfigureAwait(false);

        await foreach (var delivery in transport
            .ReceiveAsync(SampleMessagingParticipant.Identity, CancellationToken.None)
            .ConfigureAwait(false))
        {
            delivery.Headers[MessagingHeaders.MessageType]
                .Should().Be("ark_mediator_framework_sample_application_messages_process_book_print_process_request");
            delivery.Headers[MessagingHeaders.SenderIdentity]
                .Should().Be(SampleMessagingParticipant.Identity);
            codec.Deserialize<ProcessBookPrintProcessRequest>(delivery.Payload).Id.Should().NotBe(Guid.Empty);
            await delivery.CompleteAsync(default).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException("The sample bus did not produce a delivery.");
    }
}
