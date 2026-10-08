// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.Host;
using Ark.MediatorFramework.Sample.Core.Application.Messages;
using Ark.MediatorFramework.Sample.Core.Application.Services;
using Ark.MediatorFramework.Sample.Core.WebRebus.Hosting;
using Ark.MediatorFramework.Sample.Core.WebRebus.NotificationProcessor;
using Ark.Tools.MediatorFramework.Rebus;
using Ark.Tools.NLog;
using Ark.Tools.Rebus;
using Ark.Tools.Solid;

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
    NLogConfigurer.For("Ark.MediatorFramework.Sample.Core.WebRebus.NotificationProcessor")
        .WithDefaultTargetsAndRulesFromConfiguration(builder.Configuration)
        .Apply();
    builder.Logging.ClearProviders();
    builder.Logging.AddNLog();
    var sql = builder.Configuration.GetConnectionString("Sample")
        ?? throw new InvalidOperationException("ConnectionStrings:Sample is required.");
    var serviceBus = builder.Configuration.GetConnectionString("ServiceBus")
        ?? throw new InvalidOperationException("ConnectionStrings:ServiceBus is required.");
    await using var container = RebusHosting.CreateContainer(new ApplicationOptions { SqlConnectionString = sql });
    container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, RebusPrincipalContextWithFallbackProvider>();
    ApplicationComposition.RegisterNotificationSubscriber(container, new NoOpBookPrintNotificationSink());
    RebusHosting.Configure<NotificationRebusHost>(
        container,
        t => t.UseAzureServiceBus(serviceBus, SampleMessagingNotificationParticipant.Identity),
        startOutboxProcessor: false);
    using var host = builder.Build();
    container.Verify();
    container.StartBus();
    // Starting the bus does not subscribe: this subscribes the process to BookPrintCompleted.
    await NotificationRebusHost.SubscribeAsync(container.GetInstance<global::Rebus.Bus.IBus>()).ConfigureAwait(false);
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

namespace Ark.MediatorFramework.Sample.Core.WebRebus.NotificationProcessor
{
    /// <summary>Generated Rebus host for the notification subscriber.</summary>
    [ArkRebusHost(typeof(SampleMessagingNotificationParticipant))]
    public sealed partial class NotificationRebusHost;
}
