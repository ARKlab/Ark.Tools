// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Messages;
using Ark.MediatorFramework.Sample.Core.WebRebus.Hosting;
using Ark.MediatorFramework.Sample.Core.WebRebus.Processor;
using Ark.Tools.Compliance;
using Ark.Tools.MediatorFramework.Rebus;
using Ark.Tools.NLog;

using Azure.Identity;

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
    NLogConfigurer.For("Ark.MediatorFramework.Sample.Core.WebRebus.Processor")
        .WithDefaultTargetsAndRulesFromConfiguration(builder.Configuration)
        .Apply();
    builder.Logging.ClearProviders();
    builder.Logging.AddNLog();
    builder.Services.AddArkRedaction();
    // An isolated hosted service: its own application container and bus. The worker is the only receiver that drains the shared outbox table.
    builder.Services.AddRebusReceiverHost<WorkerRebusHost>(
        _ => RebusReceiverHostServiceCollectionExtensions.CreateRebusReceiverHostOptions(builder.Configuration),
        startOutboxProcessor: true);
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

namespace Ark.MediatorFramework.Sample.Core.WebRebus.Processor
{
    /// <summary>Generated Rebus host for the print worker.</summary>
    [ArkRebusHost(typeof(SampleMessagingParticipant))]
    public sealed partial class WorkerRebusHost;
}
