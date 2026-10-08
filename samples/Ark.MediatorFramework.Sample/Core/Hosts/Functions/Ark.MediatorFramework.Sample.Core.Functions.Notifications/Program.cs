// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Application.Services;
using Ark.MediatorFramework.Sample.Core.Functions.Hosting;
using Ark.Tools.AspNetCore.ApplicationInsights.Startup;
using Ark.Tools.AspNetCore.HealthChecks;
using Ark.Tools.Compliance;
using Ark.Tools.MediatorFramework.AzureFunctions.Generated;
using Ark.Tools.NLog;

using Azure.Identity;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NLog;
using NLog.Extensions.Logging;

try
{
    var builder = FunctionsApplication.CreateBuilder(args);
    // Before reading connection strings, so that Key Vault can supply them.
    var keyVaultUri = builder.Configuration["KeyVault:Uri"];
    if (Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var uri))
        builder.Configuration.AddAzureKeyVault(uri, new DefaultAzureCredential());
    NLogConfigurer.For("Ark.MediatorFramework.Sample.Core.Functions.Notifications")
        .WithDefaultTargetsAndRulesFromConfiguration(builder.Configuration, async: false)
        .Apply();
    builder.Logging.ClearProviders();
    builder.Logging.AddNLog(new NLogProviderOptions
    {
        CaptureMessageTemplates = true,
        CaptureMessageProperties = true
    });
    builder.ConfigureFunctionsWebApplication();
    builder.Services.AddArkRedaction();
    builder.Services.ArkApplicationInsightsTelemetry(builder.Configuration);

    await using var container = FunctionsHosting.CreateContainer(new ApplicationOptions
    {
        SqlConnectionString = builder.Configuration.GetConnectionString("Sample")
            ?? throw new InvalidOperationException("ConnectionStrings:Sample is required."),
    });
    ApplicationComposition.RegisterNotificationSubscriber(container, new LoggingBookPrintNotificationSink());
    FunctionsHosting.AddMessagingTrigger(
        builder.Services,
        builder.Configuration,
        ArkGeneratedMessagingFunctions.Manifest,
        container);
    builder.Services.AddArkHealthChecks();

    await builder.Build().RunAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    LogManager.GetLogger("Main").Fatal(
        ex,
        CultureInfo.InvariantCulture,
        "Unhandled startup or host failure: {Message}",
        ex.Message);
    Environment.ExitCode = 1;
}
finally
{
    LogManager.Flush(TimeSpan.FromSeconds(5));
    LogManager.Shutdown();
}
