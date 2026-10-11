// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Aot.Application;
using Ark.Tools.Compliance;
using Ark.Tools.MediatorFramework.AzureFunctions;
using Ark.Tools.MediatorFramework.AzureFunctions.Generated;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.Services.AddArkRedaction();
AotComposition.AddAotApplication(builder.Services);
builder.Services.ConfigureArkMessagingFunctions(
    builder.Configuration,
    ArkGeneratedMessagingFunctions.Manifest,
    messaging => messaging
        .UseTransport(static transport => transport.UseServiceBus())
        .UseDataBus(dataBus => dataBus.UseAzureBlob(AotComposition.DataBusOptions(builder.Configuration))));

await builder.Build().RunAsync().ConfigureAwait(false);
