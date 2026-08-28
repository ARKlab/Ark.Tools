// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.AzureFunctions;

using Ark.Tools.AspNetCore.ApplicationInsights.Startup;
using Ark.Tools.AspNetCore.HealthChecks;
using Ark.Tools.MediatorFramework.AzureFunctions.Generated;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.NLog;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NLog;
using NLog.Extensions.Logging;

using NodaTime;

namespace Ark.MediatorFramework.Sample.AuditFunctions;

/// <summary>Starts the audit subscriber Functions host.</summary>
public static class Program
{
    /// <summary>Runs the audit subscriber Functions host.</summary>
    /// <param name="args">The process arguments.</param>
    public static async Task Main(string[] args)
    {
        try
        {
            var builder = FunctionsApplication.CreateBuilder(args);
            NLogConfigurer.For("Ark.MediatorFramework.Sample.AuditFunctions")
                .WithDefaultTargetsAndRulesFromConfiguration(builder.Configuration, async: false)
                .Apply();
            builder.Logging.ClearProviders();
            builder.Logging.AddNLog();
            builder.ConfigureFunctionsWebApplication();
            builder.Services.ArkApplicationInsightsTelemetry(builder.Configuration);

            var sqlConnectionString = builder.Configuration["ConnectionStrings:Sample"];
#pragma warning disable CA2000 // The hosted service owns and disposes the container at process shutdown.
            var applicationContainer = AzureFunctionsNativeComposition.BuildContainer(
                useSqlStore: !string.IsNullOrWhiteSpace(sqlConnectionString),
                connectionString: sqlConnectionString,
                registerBookPrintNotificationHandler: false);
#pragma warning restore CA2000
            builder.Services.AddArkAzureFunctions(applicationContainer);
            builder.Services.AddArkMessagingFunctionsHost(
                applicationContainer,
                builder.Configuration,
                ArkGeneratedMessagingFunctions.Manifest,
                new InMemoryMessagingDataBus(
                    SystemClock.Instance,
                    Duration.FromHours(2)),
                MessagingFunctionsRuntimeTransport.AzureServiceBus);
            builder.Services.AddArkMessagingOutboxEnqueue();
            builder.Services.AddArkHealthChecks();
            builder.Services.AddHostedService<ContainerHostedService>();

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
    }

    private sealed class ContainerHostedService : IHostedService
    {
        private readonly SimpleInjector.Container _container;

        public ContainerHostedService(SimpleInjector.Container container)
        {
            _container = container;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }
}
