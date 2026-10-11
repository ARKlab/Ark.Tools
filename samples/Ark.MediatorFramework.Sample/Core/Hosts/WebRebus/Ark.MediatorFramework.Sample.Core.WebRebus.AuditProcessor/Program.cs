// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Application.Messages;
using Ark.MediatorFramework.Sample.Core.Application.Services;
using Ark.MediatorFramework.Sample.Core.WebRebus.AuditProcessor;
using Ark.MediatorFramework.Sample.Core.WebRebus.Hosting;
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
    NLogConfigurer.For("Ark.MediatorFramework.Sample.Core.WebRebus.AuditProcessor")
        .WithDefaultTargetsAndRulesFromConfiguration(builder.Configuration)
        .Apply();
    builder.Logging.ClearProviders();
    builder.Logging.AddNLog();
    builder.Services.AddArkRedaction();
    // An isolated hosted service: its own application container and bus.
    builder.Services.AddRebusReceiverHost<AuditRebusHost>(
        _ => RebusReceiverHostServiceCollectionExtensions.CreateRebusReceiverHostOptions(builder.Configuration),
        static container => ApplicationComposition.RegisterAuditSubscriber(container, new LoggingBookPrintAuditSink()));
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

namespace Ark.MediatorFramework.Sample.Core.WebRebus.AuditProcessor
{
    /// <summary>Generated Rebus host for the audit subscriber.</summary>
    [ArkRebusHost(typeof(SampleMessagingAuditParticipant))]
    public sealed partial class AuditRebusHost;
}
