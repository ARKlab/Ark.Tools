// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Aot.Application;
using Ark.MediatorFramework.Sample.Aot.Web.Api;
using Ark.Tools.Compliance;
using Ark.Tools.MediatorFramework.Generated;
using Ark.Tools.MediatorFramework.Messaging;

using Azure.Messaging.ServiceBus;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.AddArkRedaction();
AotComposition.AddAotApplication(builder.Services);
builder.Services.ConfigureHttpJsonOptions(static options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AotJsonContext.Default);
});
builder.Services.AddAuthorization();

var serviceBusConnection = builder.Configuration.GetConnectionString("ServiceBus")
    ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");
// No outbox: the handler sends straight to Service Bus.
builder.Services.ConfigureArkMessaging<AotMessagingNetwork>(messaging => messaging
    .Producer<AotApiParticipant>(producer => producer
        .UseTransport(transport => transport.UseServiceBus(new ServiceBusClient(serviceBusConnection)))
        .UseDataBus(dataBus => dataBus.UseAzureBlob(AotComposition.DataBusOptions(builder.Configuration)))));

var app = builder.Build();
app.UseAuthorization();
app.MapArkEndpoints<AotEndpointContext>();
await app.RunAsync().ConfigureAwait(false);
