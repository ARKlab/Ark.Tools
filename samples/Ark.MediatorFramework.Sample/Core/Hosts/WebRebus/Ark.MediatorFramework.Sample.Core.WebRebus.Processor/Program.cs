// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Application.Messages;
using Ark.MediatorFramework.Sample.Core.WebRebus.Hosting;
using Ark.MediatorFramework.Sample.Core.WebRebus.Processor;
using Ark.Tools.MediatorFramework.Rebus;
using Ark.Tools.NLog;
using Ark.Tools.Rebus;
using Ark.Tools.Solid;

using Azure.Identity;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NLog;
using NLog.Extensions.Logging;

using Rebus.Config;

using System.Security.Claims;

try
{
    var builder = Host.CreateApplicationBuilder(args);
    // Before reading connection strings, so that Key Vault can supply them.
    var keyVaultUri = builder.Configuration["KeyVault:Uri"];
    if (Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var uri))
        builder.Configuration.AddAzureKeyVault(uri, new DefaultAzureCredential());
    NLogConfigurer.For("Ark.MediatorFramework.Sample.Core.WebRebus.Processor")
        .WithDefaultTargetsAndRulesFromConfiguration(builder.Configuration)
        .Apply();
    builder.Logging.ClearProviders();
    builder.Logging.AddNLog();
    var sql = builder.Configuration.GetConnectionString("Sample")
        ?? throw new InvalidOperationException("ConnectionStrings:Sample is required.");
    var serviceBus = builder.Configuration.GetConnectionString("ServiceBus")
        ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");
    var dataBus = builder.Configuration.GetConnectionString("DataBus")
        ?? throw new InvalidOperationException("ConnectionStrings:DataBus is required.");
    await using var container = RebusHosting.CreateContainer(new ApplicationOptions { SqlConnectionString = sql });
    container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, RebusPrincipalContextWithFallbackProvider>();
    // The worker is the only process that drains the shared outbox table.
    RebusHosting.Configure<WorkerRebusHost>(
        container,
        t => t.UseAzureServiceBus(serviceBus, SampleMessagingParticipant.Identity),
        d => d.StoreInBlobStorage(dataBus, RebusHosting.DataBusContainerName),
        startOutboxProcessor: true);
    using var host = builder.Build();
    container.Verify();
    container.StartBus();
    await WorkerRebusHost.SubscribeAsync(container.GetInstance<global::Rebus.Bus.IBus>()).ConfigureAwait(false);
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

namespace Ark.MediatorFramework.Sample.Core.WebRebus.Processor
{
    /// <summary>Generated Rebus host for the print worker.</summary>
    [ArkRebusHost(typeof(SampleMessagingParticipant))]
    public sealed partial class WorkerRebusHost;
}
