// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.DAL;
using Ark.MediatorFramework.Sample.Core.Functions.OutboxProcessor;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.NLog;
using Ark.Tools.Sql.SqlServer;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NLog;
using NLog.Extensions.Logging;

try
{
    var builder = Host.CreateApplicationBuilder(args);
    NLogConfigurer.For("Ark.MediatorFramework.Sample.Core.Functions.OutboxProcessor")
        .WithDefaultTargetsAndRulesFromConfiguration(builder.Configuration)
        .Apply();
    builder.Logging.ClearProviders();
    builder.Logging.AddNLog();
    var sql = builder.Configuration.GetConnectionString("Sample")
        ?? throw new InvalidOperationException("ConnectionStrings:Sample is required.");
    var serviceBus = builder.Configuration.GetConnectionString("ServiceBus")
        ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");
#pragma warning disable CA2000 // The transport owns and disposes the Service Bus client.
    await using var transport = new ServiceBusMessagingTransport(new ServiceBusClient(serviceBus));
#pragma warning restore CA2000
    OutboxProcessorComposition.AddOutboxProcessor(
        builder.Services,
        transport,
        new SampleDataContextFactory(new SqlConnectionManager(), new SampleDataContextConfig(sql)));
    using var host = builder.Build();
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
