// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Aot.Application;
using Ark.Tools.Compliance;
using Ark.Tools.MediatorFramework.Messaging;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddArkRedaction();
AotComposition.AddAotApplication(builder.Services);

var serviceBusConnection = builder.Configuration.GetConnectionString("ServiceBus")
    ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");
var administrationConnection = builder.Configuration.GetConnectionString("ServiceBusAdministration") ?? serviceBusConnection;
builder.Services.ConfigureArkMessaging<AotMessagingNetwork>(messaging => messaging
    .Receiver<AotProcessorParticipant>(receiver => receiver
        .UseTransport(transport => transport.UseServiceBus(new ServiceBusClient(serviceBusConnection)))
        .UseDataBus(dataBus => dataBus.UseAzureBlob(AotComposition.DataBusOptions(builder.Configuration)))
        .UseResourceManagement(new ServiceBusTransportManagement(new ServiceBusAdministrationClient(administrationConnection)))));

await builder.Build().RunAsync().ConfigureAwait(false);
