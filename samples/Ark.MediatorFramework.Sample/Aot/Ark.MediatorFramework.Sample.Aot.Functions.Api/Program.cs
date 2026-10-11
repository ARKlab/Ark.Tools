// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Aot.Application;
using Ark.Tools.Compliance;
using Ark.Tools.MediatorFramework.AzureFunctions;
using Ark.Tools.MediatorFramework.Messaging;

using Azure.Messaging.ServiceBus;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.Services.AddArkRedaction();
AotComposition.AddAotApplication(builder.Services);
// HTTP binding reads the ASP.NET Core JSON options: only the source-generated context resolves types under Native AOT.
builder.Services.AddArkAzureFunctions(AotJsonContext.Default);
// App Service Easy Auth: header-based, no token validation stack in the worker.
builder.Services.AddArkAzureFunctionsEasyAuthAuthentication();

var serviceBusConnection = builder.Configuration["AzureServiceBus:ConnectionString"];
if (string.IsNullOrWhiteSpace(serviceBusConnection))
    throw new InvalidOperationException("AzureServiceBus:ConnectionString is required.");
// No outbox: the handler sends straight to Service Bus.
builder.Services.ConfigureArkMessaging<AotMessagingNetwork>(messaging => messaging
    .Producer<AotApiParticipant>(producer => producer
        .UseTransport(transport => transport.UseServiceBus(new ServiceBusClient(serviceBusConnection)))
        .UseDataBus(dataBus => dataBus.UseAzureBlob(AotComposition.DataBusOptions(builder.Configuration)))));

await builder.Build().RunAsync().ConfigureAwait(false);
