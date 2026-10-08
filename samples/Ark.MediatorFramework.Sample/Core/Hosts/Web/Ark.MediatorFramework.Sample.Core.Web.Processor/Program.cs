// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Application.Messages;
using Ark.MediatorFramework.Sample.Core.Web.Hosting;
using Ark.Tools.Compliance;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.NLog;

using Azure.Identity;
using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NLog;
using NLog.Extensions.Logging;

try
{
    var builder = Host.CreateApplicationBuilder(args);
    // Before reading connection strings, so that Key Vault can supply them.
    var keyVaultUri = builder.Configuration["KeyVault:Uri"];
    if (Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var uri))
        builder.Configuration.AddAzureKeyVault(uri, new DefaultAzureCredential());
    NLogConfigurer.For("Ark.MediatorFramework.Sample.Core.Web.Processor")
        .WithDefaultTargetsAndRulesFromConfiguration(builder.Configuration)
        .Apply();
    builder.Logging.ClearProviders();
    builder.Logging.AddNLog();
    builder.Services.AddArkRedaction();
    var sql = builder.Configuration.GetConnectionString("Sample")
        ?? throw new InvalidOperationException("ConnectionStrings:Sample is required.");
    var serviceBus = builder.Configuration.GetConnectionString("ServiceBus")
        ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");
    await using var container = WebHosting.CreateContainer(new ApplicationOptions { SqlConnectionString = sql });
#pragma warning disable CA2000 // The transport owns and disposes the Service Bus client.
    await using var transport = new ServiceBusMessagingTransport(new ServiceBusClient(serviceBus));
#pragma warning restore CA2000
    WebHosting.AddParticipant<SampleMessagingParticipant>(
        builder.Services, container, transport, WebHosting.CreateDataBus(builder.Configuration),
        WebHosting.CreateResourceManagement(builder.Configuration), receiver: true);
    IHost? built = null;
    WebHosting.BridgeBus(container, () => built?.Services
        ?? throw new InvalidOperationException("The host is not built yet."));
    using var host = builder.Build();
    built = host;
    container.Verify();
    await host.RunAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    LogManager.GetLogger("Main").Fatal(ex, CultureInfo.InvariantCulture, "Unhandled host failure: {Message}", ex.Message);
    Environment.ExitCode = 1;
}
finally
{
    LogManager.Flush(TimeSpan.FromSeconds(5));
    LogManager.Shutdown();
}
